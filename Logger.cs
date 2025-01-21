using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace AthenaSaveRelocator
{
    /// <summary>
    /// Responsible for appending text to the log file and writing to Debug.
    /// </summary>
    internal static class Logger
    {
        // If you wanted to keep "LogFileName" flexible, you could store or set it here.
        // For simplicity, we just accept it as part of each message or store it as a separate property.

        // Minimal approach: store a default log file name if desired
        private static string _logFileName = "log.txt";

        public static string LogFileName
        {
            get => _logFileName;
            set => _logFileName = value;
        }

        /// <summary>
        /// Appends the given message to log.txt (or the assigned file), plus outputs to Debug.
        /// </summary>
        public static void Log(string message)
        {
            var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            try
            {
                File.AppendAllText(_logFileName, logMessage + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // Ignore logging errors
            }
            Debug.WriteLine(logMessage);
        }
    }
}
