using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Windows.Forms;

namespace AthenaSaveRelocator
{
    /// <summary>
    /// Handles checking for updates from GitHub or another update source.
    /// </summary>
    internal class UpdateChecker
    {
        private const string VersionFileUrl = "https://raw.githubusercontent.com/YOUR_GITHUB_USERNAME/YOUR_REPO/main/version.json";
        private const string DownloadUrl = "https://github.com/YOUR_GITHUB_USERNAME/YOUR_REPO/releases/latest";

        public static readonly string CurrentVersion = "1.0.0"; // Change this when releasing new versions

        /// <summary>
        /// Checks if a newer version exists on GitHub.
        /// </summary>
        public static void CheckForUpdates()
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    string jsonData = client.DownloadString(VersionFileUrl);
                    var versionInfo = JsonSerializer.Deserialize<VersionInfo>(jsonData);

                    if (versionInfo != null && IsNewerVersion(versionInfo.Version, CurrentVersion))
                    {
                        ShowUpdateNotification(versionInfo.Version);
                    }
                    else
                    {
                        MessageBox.Show("You're using the latest version.", "No Updates Available", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error checking for updates: {ex.Message}");
            }
        }

        /// <summary>
        /// Compares version strings (e.g., "1.2.0" > "1.0.5").
        /// </summary>
        private static bool IsNewerVersion(string latestVersion, string currentVersion)
        {
            Version latest = new Version(latestVersion);
            Version current = new Version(currentVersion);
            return latest > current;
        }

        /// <summary>
        /// Displays a tray notification if an update is available.
        /// </summary>
        private static void ShowUpdateNotification(string latestVersion)
        {
            var result = MessageBox.Show(
                $"A new version ({latestVersion}) is available! Do you want to update now?",
                "Update Available",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information
            );

            if (result == DialogResult.Yes)
            {
                Process.Start(new ProcessStartInfo(DownloadUrl) { UseShellExecute = true });
            }
        }
    }

    /// <summary>
    /// Represents the JSON version file structure.
    /// </summary>
    public class VersionInfo
    {
        public string Version { get; set; }
    }
}
