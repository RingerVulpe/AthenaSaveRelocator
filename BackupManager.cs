using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace AthenaSaveRelocator
{
    /// <summary>
    /// Handles save-file backups, cleanup of old backups, and file transfers.
    /// </summary>
    internal class BackupManager
    {
        private readonly string _logFileName;
        private readonly int _maxBackups;

        public BackupManager(string logFileName, int maxBackups)
        {
            _logFileName = logFileName;
            _maxBackups = maxBackups;

            // If you want Logger to use the same file:
            Logger.LogFileName = _logFileName;
        }

        /// <summary>
        /// Creates a zip backup of all .save files in the given folder.
        /// </summary>
        public void BackupSaves(string folderToBackup)
        {
            try
            {
                var backupDir = Path.Combine(folderToBackup, "Backup");
                Directory.CreateDirectory(backupDir);

                // Remove older backups beyond the max
                CleanupOldBackups(backupDir);

                // Create new backup zip
                var timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var zipName = $"SaveBackup_{timeStamp}.zip";
                var zipPath = Path.Combine(backupDir, zipName);

                using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    var saveFiles = Directory.GetFiles(folderToBackup, "*.save");
                    foreach (var sf in saveFiles)
                    {
                        zip.CreateEntryFromFile(sf, Path.GetFileName(sf));
                    }
                }

                Logger.Log($"Backup created: {zipPath}");
            }
            catch (Exception ex)
            {
                Logger.Log($"Error creating backup in '{folderToBackup}': {ex.Message}");
            }
        }

        /// <summary>
        /// Removes older zip backups so that only _maxBackups remain.
        /// </summary>
        private void CleanupOldBackups(string backupDir)
        {
            try
            {
                var zipFiles = Directory.GetFiles(backupDir, "*.zip")
                                        .Select(f => new FileInfo(f))
                                        .OrderByDescending(fi => fi.CreationTime)
                                        .ToList();

                if (zipFiles.Count > _maxBackups)
                {
                    var toDelete = zipFiles.Skip(_maxBackups).ToList();
                    foreach (var oldZip in toDelete)
                    {
                        try
                        {
                            oldZip.Delete();
                            Logger.Log($"Deleted old backup: {oldZip.Name}");
                        }
                        catch (Exception ex)
                        {
                            Logger.Log($"Error deleting old backup {oldZip.Name}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error cleaning up old backups: {ex.Message}");
            }
        }

        /// <summary>
        /// Copies newer or missing .save files from 'source' to 'destination'.
        /// </summary>
        public void TransferFiles(string source, string destination)
        {
            try
            {
                var changedFiles = GetChangedSaveFiles(source, destination);
                if (changedFiles.Count == 0)
                {
                    Logger.Log($"No changed .save files to copy from '{source}' to '{destination}'.");
                    return;
                }

                Logger.Log($"Copying {changedFiles.Count} changed file(s) from '{source}' to '{destination}'...");

                foreach (var filePath in changedFiles)
                {
                    var fileName = Path.GetFileName(filePath);
                    var destPath = Path.Combine(destination, fileName);

                    try
                    {
                        File.Copy(filePath, destPath, true);
                        Logger.Log($"Copied '{fileName}' to '{destination}'.");
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Error copying '{fileName}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Transfer error: {ex.Message}");
                throw; // or you can show a message here if desired
            }
        }

        /// <summary>
        /// Returns a list of .save files in 'source' that are more recently modified than 
        /// those in 'destination' (or which don't exist in 'destination').
        /// </summary>
        public List<string> GetChangedSaveFiles(string source, string destination)
        {
            var sourceFiles = Directory.GetFiles(source, "*.save");
            var changedList = new List<string>();

            foreach (var srcFilePath in sourceFiles)
            {
                var fileName = Path.GetFileName(srcFilePath);
                var destFilePath = Path.Combine(destination, fileName);

                if (!File.Exists(destFilePath))
                {
                    changedList.Add(srcFilePath);
                }
                else
                {
                    var srcLastWrite = File.GetLastWriteTime(srcFilePath);
                    var destLastWrite = File.GetLastWriteTime(destFilePath);
                    if (srcLastWrite > destLastWrite)
                    {
                        changedList.Add(srcFilePath);
                    }
                }
            }
            return changedList;
        }
    }
}
