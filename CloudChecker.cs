using System.Linq;

namespace AthenaSaveRelocator
{
    /// <summary>
    /// Checks whether the cloud path has newer .save files at startup.
    /// </summary>
    internal class CloudChecker
    {
        private readonly BackupManager _backupManager;

        public CloudChecker(BackupManager backupManager)
        {
            _backupManager = backupManager;
        }

        /// <summary>
        /// Optionally, you could place a method here to check the cloud, 
        /// but in the current solution we do it directly in the form code. 
        /// 
        /// If you wanted, you'd pass the local path, cloud path, 
        /// do the same logic, and return a bool or the changed files list. 
        /// 
        /// For now, we are just leaving this as a placeholder since the form 
        /// is already calling `_backupManager.GetChangedSaveFiles(...)`.
        /// </summary>
    }
}
