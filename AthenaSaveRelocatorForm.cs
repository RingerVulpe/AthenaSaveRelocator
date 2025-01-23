using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;

namespace AthenaSaveRelocator
{
    internal class AthenaSaveRelocatorForm : Form
    {
        #region Fields

        private NotifyIcon _trayIcon;
        private ContextMenuStrip _trayMenu;
        private System.Windows.Forms.Timer _pollGameTimer;

        private string _localPath;
        private string _cloudPath;
        private string _gameProcessName;

        private bool _wasGameRunning = false;
        private Dictionary<string, DateTime> _preGameModTimes = new Dictionary<string, DateTime>();
        private DateTime _lastSyncTime = DateTime.MinValue;

        private enum BalloonMode { None, CloudNewerAtStartup, LocalChangedAfterGame }
        private BalloonMode _currentBalloonMode = BalloonMode.None;

        private const int MaxBackups = 5;
        private const string LogFileName = "log.txt";

        private BackupManager _backupManager;
        private GameWatcher _gameWatcher;
        private CloudChecker _cloudChecker;
        private BalloonNotifier _balloonNotifier;

        private System.Windows.Forms.Timer _updateCheckTimer;

        // Newly added field for process watching
        private Process _gameProcess;

        #endregion

        #region Constructor

        public AthenaSaveRelocatorForm()
        {
            // Log entry into constructor
            Logger.Log("INFO: Entering AthenaSaveRelocatorForm constructor.");

            try
            {
                // Basic form setup
                Text = "AthenaSaveRelocator (Hidden Form)";
                ShowInTaskbar = false;
                WindowState = FormWindowState.Minimized;

                // Attempt to set a safe icon
                try
                {
                    Icon = new Icon(SystemIcons.Information, 40, 40);
                }
                catch (Exception exIcon)
                {
                    Logger.Log($"WARN: Could not set custom Icon. Falling back to default. Exception: {exIcon.Message}");
                    // Even if icon fails, it's non-fatal. We do nothing special here.
                }

                // Step 1. Load configuration from pathFile.txt
                LoadConfiguration();

                // Step 2. Initialize helper objects
                _backupManager = new BackupManager(LogFileName, MaxBackups);
                _gameWatcher = new GameWatcher(_gameProcessName);
                _cloudChecker = new CloudChecker(_backupManager);
                _balloonNotifier = new BalloonNotifier();

                // Step 3. Initialize tray UI
                InitializeTray();

                // Step 4. Initialize polling timer for game detection
                InitializePollingTimer();

                // Step 5. Check whether cloud is newer right after startup
                CheckCloudNewerAtStartup();

                // Step 6. Check for updates
                try
                {
                    _ = UpdateChecker.CheckForUpdates();
                }
                catch (Exception updateEx)
                {
                    Logger.Log($"ERROR: UpdateChecker failed: {updateEx.Message}");
                }

                Logger.Log("INFO: AthenaSaveRelocatorForm constructor completed successfully.");
            }
            catch (Exception ex)
            {
                // Log any unhandled exception in constructor
                Logger.Log($"FATAL: Unhandled exception in constructor: {ex.Message}");
                MessageBox.Show(
                    $"Unhandled error initializing. The program will now exit.\n\n{ex.Message}",
                    "Startup Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                Environment.Exit(1); // Hard exit because the form cannot proceed safely
            }
        }

        #endregion

        #region Configuration

        private void LoadConfiguration()
        {
            Logger.Log("INFO: Attempting to load configuration from pathFile.txt.");

            try
            {
                if (!File.Exists("pathFile.txt"))
                {
                    Logger.Log("ERROR: pathFile.txt not found. Exiting.");
                    MessageBox.Show(
                        "pathFile.txt not found. Application will exit.",
                        "Configuration Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                    Environment.Exit(1);
                }

                var lines = File.ReadAllLines("pathFile.txt");
                if (lines.Length < 2)
                {
                    Logger.Log("ERROR: pathFile.txt missing local/cloud paths. Exiting.");
                    MessageBox.Show(
                        "pathFile.txt must contain at least two lines (local and cloud paths).",
                        "Configuration Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                    Environment.Exit(1);
                }

                _localPath = lines[0].Trim();
                _cloudPath = lines[1].Trim();
                _gameProcessName = lines.Length >= 3 ? lines[2].Trim() : string.Empty;

                // Validate directories
                if (!Directory.Exists(_localPath))
                {
                    Logger.Log($"ERROR: Local path '{_localPath}' does not exist. Exiting.");
                    MessageBox.Show(
                        $"Local path does not exist: {_localPath}",
                        "Configuration Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                    Environment.Exit(1);
                }
                if (!Directory.Exists(_cloudPath))
                {
                    Logger.Log($"ERROR: Cloud path '{_cloudPath}' does not exist. Exiting.");
                    MessageBox.Show(
                        $"Cloud path does not exist: {_cloudPath}",
                        "Configuration Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                    Environment.Exit(1);
                }

                // Check for process name
                if (string.IsNullOrWhiteSpace(_gameProcessName))
                {
                    Logger.Log("INFO: No game process name found in pathFile.txt line 3. Will not detect game running status.");
                }
                else
                {
                    Logger.Log($"INFO: Watching for game process: '{_gameProcessName}.exe'");
                }

                // Debug: Summarize loaded config
                Logger.Log($"INFO: Configuration loaded. LocalPath={_localPath}, CloudPath={_cloudPath}, GameProcessName={_gameProcessName}");
            }
            catch (Exception ex)
            {
                // Catch all other exceptions
                Logger.Log($"FATAL: Exception in LoadConfiguration: {ex.Message}");
                MessageBox.Show(
                    $"Error reading pathFile.txt: {ex.Message}\n\nApplication will exit.",
                    "Configuration Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                Environment.Exit(1);
            }
        }

        #endregion

        #region Tray Initialization

        private void InitializeTray()
        {
            Logger.Log("INFO: Initializing tray menu and tray icon.");

            try
            {
                _trayMenu = new ContextMenuStrip();
                _trayMenu.Items.Add("Backup & Upload Save", null, OnBackupAndUploadClicked);
                _trayMenu.Items.Add("Download & Restore Save", null, OnDownloadAndRestoreClicked);
                _trayMenu.Items.Add("View Logs", null, OnViewLogsClicked);
                _trayMenu.Items.Add("Check for Updates", null, OnCheckForUpdatesClicked);
                _trayMenu.Items.Add("Start/Pause Polling", null, OnEnablePollingClicked);
                _trayMenu.Items.Add("Quit App", null, OnQuitClicked);

                _trayIcon = new NotifyIcon
                {
                    Text = BuildTrayTooltip(),
                    Icon = new Icon(SystemIcons.Information, 40, 40),
                    ContextMenuStrip = _trayMenu,
                    Visible = true
                };

                _trayIcon.BalloonTipClicked += OnBalloonTipClicked;
            }
            catch (Exception ex)
            {
                Logger.Log($"ERROR: Exception initializing tray: {ex.Message}");
                MessageBox.Show(
                    $"Error initializing tray icon or menu:\n{ex.Message}",
                    "Tray Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        #endregion

        #region Polling / Timers

        private void InitializePollingTimer()
        {
            Logger.Log("INFO: Initializing polling timer for game process and update checker.");

            try
            {
                _pollGameTimer = new System.Windows.Forms.Timer
                {
                    Interval = 5000 // Poll every 5 seconds
                };
                _pollGameTimer.Tick += (s, e) =>
                {
                    try
                    {
                        PollForGameProcess();
                    }
                    catch (Exception exTimer)
                    {
                        Logger.Log($"ERROR: Exception in PollForGameProcess: {exTimer.Message}");
                    }
                };
                _pollGameTimer.Start();

                _updateCheckTimer = new System.Windows.Forms.Timer
                {
                    Interval = 24 * 60 * 60 * 1000 // 24 hours in milliseconds
                };
                _updateCheckTimer.Tick += (s, e) =>
                {
                    try
                    {
                        UpdateChecker.CheckForUpdates();
                    }
                    catch (Exception exUpdate)
                    {
                        Logger.Log($"ERROR: Exception in update check timer: {exUpdate.Message}");
                    }
                };
                _updateCheckTimer.Start();
            }
            catch (Exception ex)
            {
                Logger.Log($"ERROR: Exception during InitializePollingTimer: {ex.Message}");
                MessageBox.Show(
                    $"Failed to set up timers. The application may be unstable.\n\n{ex.Message}",
                    "Timer Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
            }
        }

        private void PollForGameProcess()
        {
            if (!_wasGameRunning)
            {
                Logger.Log("DEBUG: Game is not flagged as running; attempting to detect startup...");
                StartWatchingGameProcess();
            }
            // If the process is running, exit is handled by OnGameProcessExited
        }

        private void StartWatchingGameProcess()
        {
            if (string.IsNullOrWhiteSpace(_gameProcessName))
            {
                // No process to watch
                Logger.Log("DEBUG: No _gameProcessName was set; skipping process watch attempt.");
                return;
            }

            try
            {
                var process = _gameWatcher.GetGameProcess();
                if (process != null)
                {
                    _gameProcess = process;
                    _gameProcess.EnableRaisingEvents = true;
                    _gameProcess.Exited += OnGameProcessExited;

                    // Record the current snapshot of local saves
                    _preGameModTimes = TakeSaveFileSnapshot(_localPath);
                    _wasGameRunning = true;
                    Logger.Log($"INFO: Detected '{_gameProcessName}.exe' started. Capturing local save timestamps.");

                    UpdateTrayTooltip();
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"ERROR: Failed to start watching game process: {ex.Message}");
            }
        }

        private void OnGameProcessExited(object sender, EventArgs e)
        {
            Logger.Log($"INFO: Detected '{_gameProcessName}.exe' has exited.");

            try
            {
                if (_gameProcess != null)
                {
                    _gameProcess.Exited -= OnGameProcessExited;
                    _gameProcess.Dispose();
                    _gameProcess = null;
                }

                _wasGameRunning = false;

                var postGameModTimes = TakeSaveFileSnapshot(_localPath);
                bool isAnyChanged = false;

                // Compare each file for changes
                foreach (var kvp in postGameModTimes)
                {
                    string file = kvp.Key;
                    DateTime newTime = kvp.Value;

                    if (!_preGameModTimes.ContainsKey(file) || newTime > _preGameModTimes[file])
                    {
                        isAnyChanged = true;
                        break;
                    }
                }

                if (isAnyChanged)
                {
                    Logger.Log("INFO: Local save files changed after game exit. Showing balloon notification.");
                    _currentBalloonMode = BalloonMode.LocalChangedAfterGame;

                    _balloonNotifier.ShowNotification(
                        _trayIcon,
                        "AthenaSaveRelocator",
                        "Game closed - new local saves found. Click here to upload.",
                        6000
                    );
                }
                else
                {
                    Logger.Log("INFO: No local save changes detected after game exit.");
                }

                UpdateTrayTooltip();
            }
            catch (Exception ex)
            {
                Logger.Log($"ERROR: Exception in OnGameProcessExited: {ex.Message}");
            }
        }

        #endregion

        #region Balloon Checks at Startup

        private void CheckCloudNewerAtStartup()
        {
            Logger.Log("INFO: Checking if cloud saves are newer than local at startup.");

            try
            {
                var changedFiles = _backupManager.GetChangedSaveFiles(_cloudPath, _localPath);
                if (changedFiles.Any())
                {
                    Logger.Log("INFO: Cloud is newer than local for at least one save. Notifying user at startup.");
                    _currentBalloonMode = BalloonMode.CloudNewerAtStartup;

                    _balloonNotifier.ShowNotification(
                        _trayIcon,
                        "AthenaSaveRelocator",
                        "We found newer cloud saves. Click here to restore them.",
                        6000
                    );
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"ERROR: Exception in CheckCloudNewerAtStartup: {ex.Message}");
            }
        }

        #endregion

        #region Tray Event Handlers

        private void OnBackupAndUploadClicked(object sender, EventArgs e)
        {
            Logger.Log("INFO: User clicked 'Backup & Upload Save'.");
            DoBackupAndUpload();
        }

        private void OnDownloadAndRestoreClicked(object sender, EventArgs e)
        {
            Logger.Log("INFO: User clicked 'Download & Restore Save'.");
            DoDownloadAndRestore();
        }

        private void OnViewLogsClicked(object sender, EventArgs e)
        {
            Logger.Log("INFO: User clicked 'View Logs'. Attempting to open log file.");
            try
            {
                Process.Start("notepad.exe", LogFileName);
            }
            catch (Exception ex)
            {
                Logger.Log($"ERROR: Error opening log file: {ex.Message}");
                MessageBox.Show(
                    $"Could not open log file. {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private void OnCheckForUpdatesClicked(object sender, EventArgs e)
        {
            Logger.Log("INFO: User clicked 'Check for Updates'.");
            try
            {
                UpdateChecker.CheckForUpdates();
            }
            catch (Exception ex)
            {
                Logger.Log($"ERROR: Exception checking for updates from menu: {ex.Message}");
                MessageBox.Show(
                    $"Check for Updates failed:\n{ex.Message}",
                    "Update Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private void OnEnablePollingClicked(object sender, EventArgs e)
        {
            Logger.Log("INFO: User toggled polling timer.");

            try
            {
                if (_pollGameTimer.Enabled)
                {
                    _pollGameTimer.Stop();
                    Logger.Log("INFO: Polling timer stopped by user.");
                }
                else
                {
                    _pollGameTimer.Start();
                    Logger.Log("INFO: Polling timer started by user.");
                }

                UpdateTrayTooltip();
            }
            catch (Exception ex)
            {
                Logger.Log($"ERROR: Exception toggling polling timer: {ex.Message}");
            }
        }

        private void OnQuitClicked(object sender, EventArgs e)
        {
            Logger.Log("INFO: User clicked 'Quit App'. Exiting application.");
            Application.Exit();
        }

        #endregion

        #region BalloonTip Click Handler

        private void OnBalloonTipClicked(object sender, EventArgs e)
        {
            Logger.Log($"INFO: Balloon tip clicked with mode={_currentBalloonMode}.");

            try
            {
                switch (_currentBalloonMode)
                {
                    case BalloonMode.CloudNewerAtStartup:
                        {
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
                                Logger.Log("INFO: User dismissed 'cloud newer' prompt at startup.");
                            }
                            break;
                        }
                    case BalloonMode.LocalChangedAfterGame:
                        {
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
                                Logger.Log("INFO: User dismissed 'local changed' upload prompt.");
                            }
                            break;
                        }
                    default:
                        // No action
                        Logger.Log("DEBUG: Balloon tip clicked but no special mode set.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"ERROR: Exception in OnBalloonTipClicked: {ex.Message}");
            }
            finally
            {
                _currentBalloonMode = BalloonMode.None;
            }
        }

        #endregion

        #region Core Backup/Restore

        private void DoBackupAndUpload()
        {
            Logger.Log("INFO: Starting backup and upload procedure.");

            if (_wasGameRunning)
            {
                Logger.Log("WARN: Attempted backup/upload while game is running; blocking.");
                MessageBox.Show(
                    "Game is currently running. Please close the game before transferring files.",
                    "Game Running",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                return;
            }

            try
            {
                // 1. Back up local
                _backupManager.BackupSaves(_localPath);

                // 2. Transfer local -> cloud
                _backupManager.TransferFiles(_localPath, _cloudPath);

                _lastSyncTime = DateTime.Now;
                Logger.Log("INFO: Backup & Upload completed successfully.");
                MessageBox.Show(
                    "Backup and upload to cloud complete.",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                Logger.Log($"ERROR: Exception in DoBackupAndUpload: {ex.Message}");
                MessageBox.Show(
                    $"Failed to upload saves.\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            finally
            {
                UpdateTrayTooltip();
            }
        }

        private void DoDownloadAndRestore()
        {
            Logger.Log("INFO: Starting download and restore procedure.");

            if (_wasGameRunning)
            {
                Logger.Log("WARN: Attempted download/restore while game is running; blocking.");
                MessageBox.Show(
                    "Game is currently running. Please close the game before transferring files.",
                    "Game Running",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                return;
            }

            try
            {
                // 1. Back up local first
                _backupManager.BackupSaves(_localPath);

                // 2. Transfer cloud -> local
                _backupManager.TransferFiles(_cloudPath, _localPath);

                _lastSyncTime = DateTime.Now;
                Logger.Log("INFO: Download & Restore completed successfully.");
                MessageBox.Show(
                    "Download and restore from cloud complete.",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                Logger.Log($"ERROR: Exception in DoDownloadAndRestore: {ex.Message}");
                MessageBox.Show(
                    $"Failed to download/restore saves.\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            finally
            {
                UpdateTrayTooltip();
            }
        }

        #endregion

        #region Utility

        /// <summary>
        /// Captures the last-write time of all *.save files in the given folder.
        /// </summary>
        private Dictionary<string, DateTime> TakeSaveFileSnapshot(string folder)
        {
            var snapshot = new Dictionary<string, DateTime>();
            Logger.Log($"DEBUG: Taking snapshot of .save files in '{folder}'.");

            try
            {
                var files = Directory.GetFiles(folder, "*.save");
                foreach (var filePath in files)
                {
                    snapshot[filePath] = File.GetLastWriteTime(filePath);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"ERROR: Error taking save file snapshot in '{folder}': {ex.Message}");
            }

            return snapshot;
        }

        private string BuildTrayTooltip()
        {
            DateTime tempLastSyncTime;
            bool isSynced = AreSavesSynced(out tempLastSyncTime);

            string lastSyncStr = isSynced
                ? $"🟢 Synced {tempLastSyncTime:yyyy-MM-dd HH:mm}"
                : "No sync yet";

            string gameStatus = "No Game Monitoring";
            if (!string.IsNullOrWhiteSpace(_gameProcessName))
            {
                gameStatus = _wasGameRunning ? "Game Running" : "Game Not Running";
            }

            // Polling status
            string pollingStatus = _pollGameTimer != null && _pollGameTimer.Enabled
                ? "Polling Enabled"
                : "Polling Disabled";

            // Return multiline tooltip
            return $"AthenaSaveRelocator\n{gameStatus}\nLast Sync: {lastSyncStr}\n{pollingStatus}";
        }

        private bool AreSavesSynced(out DateTime lastSyncTime)
        {
            lastSyncTime = DateTime.MinValue;

            try
            {
                var localFiles = Directory.GetFiles(_localPath, "*.save");
                var cloudFiles = Directory.GetFiles(_cloudPath, "*.save");

                // Quick check: if counts differ, definitely not synced
                if (localFiles.Length != cloudFiles.Length)
                {
                    return false;
                }

                // Compare each file
                foreach (var localFile in localFiles)
                {
                    string cloudFile = System.IO.Path.Combine(_cloudPath, Path.GetFileName(localFile));
                    if (!File.Exists(cloudFile))
                    {
                        return false;
                    }

                    DateTime localMod = File.GetLastWriteTime(localFile);
                    DateTime cloudMod = File.GetLastWriteTime(cloudFile);
                    if (localMod != cloudMod)
                    {
                        return false;
                    }
                }

                // If we get here, they appear to be synced. We'll just take the local file's timestamp.
                if (localFiles.Length > 0)
                {
                    lastSyncTime = File.GetLastWriteTime(localFiles[0]);
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"ERROR: Exception in AreSavesSynced check: {ex.Message}");
                return false;
            }
        }

        private void UpdateTrayTooltip()
        {
            try
            {
                _trayIcon.Text = BuildTrayTooltip();
            }
            catch (Exception ex)
            {
                Logger.Log($"ERROR: Could not update tray tooltip: {ex.Message}");
            }
        }

        #endregion

        #region Cleanup

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Logger.Log("INFO: OnFormClosing triggered. Cleaning up resources.");

            try
            {
                // Clean up tray icon
                if (_trayIcon != null)
                {
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                }

                // Stop & dispose timers
                if (_pollGameTimer != null)
                {
                    _pollGameTimer.Stop();
                    _pollGameTimer.Dispose();
                }
                if (_updateCheckTimer != null)
                {
                    _updateCheckTimer.Stop();
                    _updateCheckTimer.Dispose();
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"ERROR: Exception in OnFormClosing cleanup: {ex.Message}");
            }
            finally
            {
                base.OnFormClosing(e);
            }
        }

        #endregion
    }
}
