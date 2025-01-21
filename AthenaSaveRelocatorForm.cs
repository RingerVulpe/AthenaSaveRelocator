﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace AthenaSaveRelocator
{
    internal class AthenaSaveRelocatorForm : Form
    {
        #region Fields

        // Tray and menu
        private NotifyIcon _trayIcon;
        private ContextMenuStrip _trayMenu;
        private System.Windows.Forms.Timer _pollGameTimer;

        // Paths and game info
        private string _localPath;
        private string _cloudPath;
        private string _gameProcessName;

        // State tracking
        private bool _wasGameRunning = false;
        private Dictionary<string, DateTime> _preGameModTimes = new Dictionary<string, DateTime>();
        private DateTime _lastSyncTime = DateTime.MinValue;

        // For balloon tips
        private enum BalloonMode { None, CloudNewerAtStartup, LocalChangedAfterGame }
        private BalloonMode _currentBalloonMode = BalloonMode.None;

        // Constants
        private const int MaxBackups = 5;
        private const string LogFileName = "log.txt";

        // Dedicated helper objects
        private BackupManager _backupManager;
        private GameWatcher _gameWatcher;
        private CloudChecker _cloudChecker;
        private BalloonNotifier _balloonNotifier;

        #endregion

        #region Constructor and Initialization

        public AthenaSaveRelocatorForm()
        {
            // Form configuration
            Text = "AthenaSaveRelocator (Hidden Form)";
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;

            // 1. Load config from pathFile.txt
            LoadConfiguration();

            // 2. Initialize dedicated helper objects
            _backupManager = new BackupManager(LogFileName, MaxBackups);
            _gameWatcher = new GameWatcher(_gameProcessName);
            _cloudChecker = new CloudChecker(_backupManager);
            _balloonNotifier = new BalloonNotifier();

            // 3. Initialize tray UI
            InitializeTray();

            // 4. Initialize polling timer for game detection
            InitializePollingTimer();

            // 5. Check whether cloud is newer right after startup
            CheckCloudNewerAtStartup();
        }

        private void LoadConfiguration()
        {
            if (!File.Exists("pathFile.txt"))
            {
                MessageBox.Show("pathFile.txt not found. Application will exit.",
                                "Configuration Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                Logger.Log("ERROR: pathFile.txt not found. Exiting.");
                Environment.Exit(1);
            }

            var lines = File.ReadAllLines("pathFile.txt");
            if (lines.Length < 2)
            {
                MessageBox.Show("pathFile.txt must contain at least two lines (local and cloud paths).",
                                "Configuration Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                Logger.Log("ERROR: pathFile.txt missing local/cloud paths. Exiting.");
                Environment.Exit(1);
            }

            _localPath = lines[0].Trim();
            _cloudPath = lines[1].Trim();
            _gameProcessName = lines.Length >= 3 ? lines[2].Trim() : string.Empty;

            if (!Directory.Exists(_localPath))
            {
                MessageBox.Show($"Local path does not exist: {_localPath}",
                                "Configuration Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                Logger.Log($"ERROR: Local path '{_localPath}' does not exist. Exiting.");
                Environment.Exit(1);
            }
            if (!Directory.Exists(_cloudPath))
            {
                MessageBox.Show($"Cloud path does not exist: {_cloudPath}",
                                "Configuration Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                Logger.Log($"ERROR: Cloud path '{_cloudPath}' does not exist. Exiting.");
                Environment.Exit(1);
            }

            if (string.IsNullOrWhiteSpace(_gameProcessName))
            {
                Logger.Log("No game process name found in pathFile.txt line 3. Will not detect game running status.");
            }
            else
            {
                Logger.Log($"Watching for game process: '{_gameProcessName}.exe'");
            }

            Logger.Log($"Configuration loaded. LocalPath={_localPath}, CloudPath={_cloudPath}, GameProcessName={_gameProcessName}");
        }

        private void InitializeTray()
        {
            _trayMenu = new ContextMenuStrip();
            _trayMenu.Items.Add("Backup & Upload Save", null, OnBackupAndUploadClicked);
            _trayMenu.Items.Add("Download & Restore Save", null, OnDownloadAndRestoreClicked);
            _trayMenu.Items.Add("View Logs", null, OnViewLogsClicked);
            _trayMenu.Items.Add("Quit App", null, OnQuitClicked);

            _trayIcon = new NotifyIcon
            {
                Text = BuildTrayTooltip(),
                Icon = SystemIcons.Information,
                ContextMenuStrip = _trayMenu,
                Visible = true
            };

            // On balloon tip click, we decide how to respond
            _trayIcon.BalloonTipClicked += OnBalloonTipClicked;

            // Update tooltip each time mouse hovers (to reflect last sync time etc.)
            _trayIcon.MouseMove += (s, e) => { _trayIcon.Text = BuildTrayTooltip(); };
        }

        private void InitializePollingTimer()
        {
            _pollGameTimer = new System.Windows.Forms.Timer();
            _pollGameTimer.Interval = 5000; // poll every 5 seconds
            _pollGameTimer.Tick += (s, e) => PollForGameProcess();
            _pollGameTimer.Start();
        }

        private void CheckCloudNewerAtStartup()
        {
            // Re-use the "GetChangedSaveFiles" approach from BackupManager
            var changedFiles = _backupManager.GetChangedSaveFiles(_cloudPath, _localPath);
            if (changedFiles.Any())
            {
                Logger.Log("Cloud is newer than local for at least one save. Notifying user at startup.");
                _currentBalloonMode = BalloonMode.CloudNewerAtStartup;

                // Show a balloon tip for 6 seconds
                _balloonNotifier.ShowBalloonNotification(
                    _trayIcon,
                    "AthenaSaveRelocator",
                    "We found newer cloud saves. Click here to restore them.",
                    6000
                );
            }
        }

        #endregion

        #region Polling for Game Process

        private void PollForGameProcess()
        {
            if (string.IsNullOrWhiteSpace(_gameProcessName))
            {
                // No process to watch
                return;
            }

            bool isRunning = _gameWatcher.IsGameRunning();
            // Transition: Not Running -> Running
            if (isRunning && !_wasGameRunning)
            {
                // Record the current snapshot of local saves
                _preGameModTimes = TakeSaveFileSnapshot(_localPath);
                _wasGameRunning = true;
                Logger.Log($"Detected '{_gameProcessName}.exe' started. Capturing local save timestamps.");
            }

            // Transition: Running -> Not Running
            if (!isRunning && _wasGameRunning)
            {
                _wasGameRunning = false;
                Logger.Log($"Detected '{_gameProcessName}.exe' exited. Checking for changed local saves...");

                var postGameModTimes = TakeSaveFileSnapshot(_localPath);
                bool anyChanged = false;

                // Compare each file
                foreach (var kvp in postGameModTimes)
                {
                    string file = kvp.Key;
                    DateTime newTime = kvp.Value;

                    if (!_preGameModTimes.ContainsKey(file) || newTime > _preGameModTimes[file])
                    {
                        anyChanged = true;
                        break;
                    }
                }

                if (anyChanged)
                {
                    Logger.Log("Detected updated local save files after game exit. Showing balloon notification.");
                    _currentBalloonMode = BalloonMode.LocalChangedAfterGame;

                    _balloonNotifier.ShowBalloonNotification(
                        _trayIcon,
                        "AthenaSaveRelocator",
                        "Game closed - new local saves found. Click here to upload.",
                        6000
                    );
                }
                else
                {
                    Logger.Log("No local save changes detected after game exit.");
                }
            }
        }

        #endregion

        #region Tray Menu Handlers

        private void OnBackupAndUploadClicked(object sender, EventArgs e)
        {
            DoBackupAndUpload();
        }

        private void OnDownloadAndRestoreClicked(object sender, EventArgs e)
        {
            DoDownloadAndRestore();
        }

        private void OnViewLogsClicked(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start("notepad.exe", LogFileName);
            }
            catch (Exception ex)
            {
                Logger.Log($"Error opening log file: {ex.Message}");
                MessageBox.Show($"Could not open log file. {ex.Message}",
                                "Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
            }
        }

        private void OnQuitClicked(object sender, EventArgs e)
        {
            Application.Exit();
        }

        #endregion

        #region Balloon Tip Click Handler

        private void OnBalloonTipClicked(object sender, EventArgs e)
        {
            // Decide what to do based on _currentBalloonMode
            switch (_currentBalloonMode)
            {
                case BalloonMode.CloudNewerAtStartup:
                    // Offer to restore from cloud
                    var resultCloud = MessageBox.Show(
                        "We found newer cloud saves. Download (restore) them now?",
                        "Download Confirmation",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question
                    );
                    if (resultCloud == DialogResult.Yes)
                    {
                        DoDownloadAndRestore();
                    }
                    else
                    {
                        Logger.Log("User dismissed 'cloud newer' prompt at startup.");
                    }
                    break;

                case BalloonMode.LocalChangedAfterGame:
                    // Offer to upload local changes
                    var resultLocal = MessageBox.Show(
                        "We detected updated local saves after the game closed. Upload them now?",
                        "Upload Confirmation",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question
                    );
                    if (resultLocal == DialogResult.Yes)
                    {
                        DoBackupAndUpload();
                    }
                    else
                    {
                        Logger.Log("User dismissed 'local changed' upload prompt.");
                    }
                    break;

                default:
                    // No action
                    break;
            }

            // Reset balloon mode
            _currentBalloonMode = BalloonMode.None;
        }

        #endregion

        #region Backup and Transfer Helpers (delegating to BackupManager)

        private void DoBackupAndUpload()
        {
            if (_wasGameRunning)
            {
                MessageBox.Show("Game is currently running. Please close the game before transferring files.",
                                "Game Running",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                Logger.Log("User tried to do Backup & Upload while game is running - blocked.");
                return;
            }

            // 1. Back up local
            _backupManager.BackupSaves(_localPath);

            // 2. Transfer local -> cloud
            _backupManager.TransferFiles(_localPath, _cloudPath);

            _lastSyncTime = DateTime.Now;
            Logger.Log("Manual/Auto Backup & Upload completed.");
            MessageBox.Show("Backup and upload to cloud complete.",
                            "Success",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
        }

        private void DoDownloadAndRestore()
        {
            if (_wasGameRunning)
            {
                MessageBox.Show("Game is currently running. Please close the game before transferring files.",
                                "Game Running",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                Logger.Log("User tried to do Download & Restore while game is running - blocked.");
                return;
            }

            // 1. Back up local before overwriting
            _backupManager.BackupSaves(_localPath);

            // 2. Transfer cloud -> local
            _backupManager.TransferFiles(_cloudPath, _localPath);

            _lastSyncTime = DateTime.Now;
            Logger.Log("Manual Download & Restore completed.");
            MessageBox.Show("Download and restore from cloud complete.",
                            "Success",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Captures the last-write time of all *.save files in the given folder.
        /// </summary>
        private Dictionary<string, DateTime> TakeSaveFileSnapshot(string folder)
        {
            var snapshot = new Dictionary<string, DateTime>();
            try
            {
                var files = Directory.GetFiles(folder, "*.save");
                foreach (var f in files)
                {
                    snapshot[f] = File.GetLastWriteTime(f);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error taking save file snapshot in '{folder}': {ex.Message}");
            }
            return snapshot;
        }

        private string BuildTrayTooltip()
        {
            var lastSyncStr = (_lastSyncTime == DateTime.MinValue)
                ? "No sync yet"
                : _lastSyncTime.ToString("yyyy-MM-dd HH:mm");

            var gameStatus = "No Game Monitoring";
            if (!string.IsNullOrWhiteSpace(_gameProcessName))
            {
                gameStatus = _wasGameRunning ? "Game Running" : "Game Not Running";
            }

            return $"AthenaSaveRelocator\n{gameStatus}\nLast Sync: {lastSyncStr}";
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Clean up tray icon
            _trayIcon.Visible = false;
            _trayIcon.Dispose();

            // Stop polling
            if (_pollGameTimer != null)
            {
                _pollGameTimer.Stop();
                _pollGameTimer.Dispose();
            }

            base.OnFormClosing(e);
        }

        #endregion
    }
}
