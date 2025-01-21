using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Compression;

namespace AthenaSaveRelocator
{
    /// <summary>
    /// Handles checking for updates from GitHub or another update source.
    /// </summary>
    internal class UpdateChecker
    {
        private const string VersionFileUrl = "https://raw.githubusercontent.com/RingerVulpe/AthenaSaveRelocator/main/version.json"; // Corrected URL
        private const string ApiUrl = "https://api.github.com/repos/RingerVulpe/AthenaSaveRelocator/releases/latest";

        private static readonly HttpClient _httpClient = new HttpClient();

        public static readonly string CurrentVersion = "1.1.1"; // Change this before release

        /// <summary>
        /// Checks if a newer version exists on GitHub.
        /// </summary>
        public static async Task CheckForUpdates()
        {
            try
            {
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("AthenaSaveRelocator-Updater");

                string jsonData = await _httpClient.GetStringAsync(VersionFileUrl);
                var versionInfo = JsonSerializer.Deserialize<VersionInfoData>(jsonData);

                if (versionInfo != null && IsNewerVersion(versionInfo.Version, CurrentVersion))
                {
                    await ShowUpdateNotification(versionInfo.Version);
                }
                else
                {
                    MessageBox.Show("You're using the latest version.", "No Updates Available", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
        private static async Task ShowUpdateNotification(string latestVersion)
        {
            var result = MessageBox.Show(
                $"A new version ({latestVersion}) is available! Do you want to update now?",
                "Update Available",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information
            );

            if (result == DialogResult.Yes)
            {
                await DownloadAndUpdate();
            }
        }

        /// <summary>
        /// Downloads and updates the application with the latest version.
        /// </summary>
        private static async Task DownloadAndUpdate()
        {
            string tempPath = Path.Combine(Path.GetTempPath(), "AthenaUpdate.zip");
            string extractPath = Path.Combine(Path.GetTempPath(), "AthenaUpdate");

            try
            {
                string jsonData = await _httpClient.GetStringAsync(ApiUrl);
                var releaseData = JsonSerializer.Deserialize<GithubRelease>(jsonData);

                if (releaseData == null || releaseData.assets.Length == 0)
                {
                    MessageBox.Show("No update found!", "Update Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Find the correct asset URL (ZIP file)
                string? zipUrl = releaseData.assets
                    .FirstOrDefault(a => a.name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))?.browser_download_url;

                if (string.IsNullOrEmpty(zipUrl))
                {
                    MessageBox.Show("Update ZIP not found!", "Update Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Download the ZIP file
                byte[] fileBytes = await _httpClient.GetByteArrayAsync(zipUrl);
                await File.WriteAllBytesAsync(tempPath, fileBytes);

                if (Directory.Exists(extractPath))
                    Directory.Delete(extractPath, true);

                ZipFile.ExtractToDirectory(tempPath, extractPath);

                // Replace the executable
                string newExePath = Path.Combine(extractPath, "AthenaSaveRelocator.exe");
                string currentExePath = Application.ExecutablePath;

                Process.Start(new ProcessStartInfo("cmd", $"/c timeout 3 && move \"{newExePath}\" \"{currentExePath}\" && start \"{currentExePath}\"")
                {
                    WindowStyle = ProcessWindowStyle.Hidden
                });

                Application.Exit();
            }
            catch (Exception ex)
            {
                Logger.Log($"Error downloading update: {ex.Message}");
                MessageBox.Show($"Update failed: {ex.Message}", "Update Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Represents the GitHub API response for latest release.
        /// </summary>
        public class GithubRelease
        {
            public string tag_name { get; set; }
            public GithubAsset[] assets { get; set; }
        }

        /// <summary>
        /// Represents an asset (file) in a GitHub release.
        /// </summary>
        public class GithubAsset
        {
            public string name { get; set; }
            public string browser_download_url { get; set; }
        }
    }

    /// <summary>
    /// Represents the version data from version.json
    /// </summary>
    public class VersionInfoData
    {
        public string Version { get; set; }
    }
}
