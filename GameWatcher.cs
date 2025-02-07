﻿using System;
using System.Diagnostics;
using System.Linq;

namespace AthenaSaveRelocator
{
    /// <summary>
    /// Checks if the target game process is running.
    /// </summary>
    internal class GameWatcher
    {
        private readonly string _gameProcessName;

        public GameWatcher(string gameProcessName)
        {
            _gameProcessName = gameProcessName;
        }

        /// <summary>
        /// Returns true if the given process is found in the system process list.
        /// </summary>
        public bool IsGameRunning()
        {
            if (string.IsNullOrWhiteSpace(_gameProcessName))
                return false;

            try
            {
                var procs = Process.GetProcessesByName(_gameProcessName);
                return procs.Any();
            }
            catch (Exception ex)
            {
                Logger.Log($"ERROR checking game process '{_gameProcessName}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Returns the running game process if found; otherwise null.
        /// </summary>
        public Process GetGameProcess()
        {
            if (string.IsNullOrWhiteSpace(_gameProcessName))
                return null;

            try
            {
                var processes = Process.GetProcessesByName(_gameProcessName);
                return processes.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Logger.Log($"ERROR getting game process '{_gameProcessName}': {ex.Message}");
                return null;
            }
        }
    }
}
