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
    internal class UpdateChecker
    {
        private const string VersionFileUrl = "https://raw.githubusercontent.com/RingerVulpe/AthenaSaveRelocator/main/version.json";
        private const string ApiUrl = "https://api.github.com/repos/RingerVulpe/AthenaSaveRelocator/releases/latest";
        private static readonly HttpClient _httpClient = new HttpClient();

        public static readonly string CurrentVersion = VersionInfo.CurrentVersion;

        public static async Task CheckForUpdates()
        {
            try
            {
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("AthenaSaveRelocator-Updater");
                string jsonUrl = VersionFileUrl + "?t=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                Logger.Log("Checking for updates...");
                string jsonData = await _httpClient.GetStringAsync(jsonUrl);

                if (string.IsNullOrWhiteSpace(jsonData))
                {
                    Logger.Log("Error: Received empty response from version.json");
                    return;
                }

                Logger.Log($"Raw JSON response: {jsonData}");

                VersionInfoData versionInfo;
                try
                {
                    versionInfo = JsonSerializer.Deserialize<VersionInfoData>(jsonData);
                }
                catch (JsonException jsonEx)
                {
                    Logger.Log($"JSON Parsing Error: {jsonEx.Message}");
                    return;
                }

                if (versionInfo == null || string.IsNullOrEmpty(versionInfo.Version))
                {
                    Logger.Log("Error: JSON is valid but missing 'Version' property.");
                    return;
                }

                Logger.Log($"Latest version found: {versionInfo.Version}, Current version: {CurrentVersion}");

                if (IsNewerVersion(versionInfo.Version, CurrentVersion))
                {
                    Logger.Log("New update available! Prompting user...");
                    await ShowUpdateNotification(versionInfo.Version);
                }
                else
                {
                    Logger.Log("You're already using the latest version.");
                    MessageBox.Show("You're using the latest version.", "No Updates Available", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (HttpRequestException ex)
            {
                Logger.Log($"HTTP Error while checking for updates: {ex.Message}");
            }
            catch (Exception ex)
            {
                Logger.Log($"Unexpected error while checking for updates: {ex.Message}");
            }
        }

        private static bool IsNewerVersion(string latestVersion, string currentVersion)
        {
            try
            {
                Version latest = new Version(latestVersion);
                Version current = new Version(currentVersion);
                return latest > current;
            }
            catch (Exception ex)
            {
                Logger.Log($"Version comparison error: {ex.Message}");
                return false;
            }
        }

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
                Logger.Log("User accepted the update.");
                await DownloadAndUpdate();
            }
            else
            {
                Logger.Log("User declined the update.");
            }
        }

        private static async Task DownloadAndUpdate()
        {
            string tempPath = Path.Combine(Path.GetTempPath(), "AthenaUpdate.zip");
            string extractPath = Path.Combine(Path.GetTempPath(), "AthenaUpdate");
            string currentExePath = Application.ExecutablePath;
            string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string exeName = Path.GetFileName(currentExePath);
            string parentDir = Directory.GetParent(appDirectory)?.FullName
                               ?? throw new Exception("Unable to determine parent directory.");

            try
            {
                Logger.Log("Fetching latest release from GitHub...");
                string jsonData = await _httpClient.GetStringAsync(ApiUrl);

                if (string.IsNullOrWhiteSpace(jsonData))
                {
                    Logger.Log("Error: GitHub API returned an empty response.");
                    return;
                }

                var releaseData = JsonSerializer.Deserialize<GithubRelease>(jsonData);
                if (releaseData?.assets == null || releaseData.assets.Length == 0)
                {
                    Logger.Log("Error: Failed to fetch latest release data or no assets found.");
                    return;
                }

                string? zipUrl = releaseData.assets
                    .FirstOrDefault(a => a.name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))?.browser_download_url;

                if (string.IsNullOrEmpty(zipUrl))
                {
                    Logger.Log("Error: Update ZIP file not found in the release assets.");
                    return;
                }

                Logger.Log("Downloading update...");
                byte[] fileBytes = await _httpClient.GetByteArrayAsync(zipUrl);
                await File.WriteAllBytesAsync(tempPath, fileBytes);

                if (Directory.Exists(extractPath))
                {
                    Directory.Delete(extractPath, true);
                }

                ZipFile.ExtractToDirectory(tempPath, extractPath);
                Logger.Log("Update extracted successfully.");

                // Identify if there's a new folder in the extracted update
                var newDirectories = Directory.GetDirectories(extractPath);
                string updaterScript = Path.Combine(extractPath, "Updater.bat");
                string scriptContent;

                if (newDirectories.Length > 0)
                {
                    // Use the first directory found as the new folder
                    string newFolderPath = newDirectories[0];
                    string newFolderName = Path.GetFileName(newFolderPath);

                    // Create an updater script to move the new folder, remove the old one, and start the new exe with --updated
                    scriptContent = $@"
@echo off
timeout /t 3 /nobreak >nul
move ""{Path.Combine(extractPath, newFolderName)}"" ""{Path.Combine(parentDir, newFolderName)}""
rmdir /S /Q ""{appDirectory}""
start """" ""{Path.Combine(parentDir, newFolderName, exeName)}"" --updated
exit
";
                }
                else
                {
                    // Fallback: If no new folder found, copy files into the current directory and start with --updated
                    scriptContent = $@"
@echo off
timeout /t 3 /nobreak >nul
xcopy /Y /E /C /I ""{extractPath}"" ""{appDirectory}""
start """" ""{currentExePath}"" --updated
exit
";
                }

                File.WriteAllText(updaterScript, scriptContent);
                Logger.Log("Launching updater script and exiting...");
                Process.Start(new ProcessStartInfo
                {
                    FileName = updaterScript,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });

                Application.Exit();
            }
            catch (Exception ex)
            {
                Logger.Log($"Error during update process: {ex.Message}");
                MessageBox.Show($"Update failed: {ex.Message}", "Update Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }



        public class GithubRelease
        {
            public string tag_name { get; set; }
            public GithubAsset[] assets { get; set; }
        }

        public class GithubAsset
        {
            public string name { get; set; }
            public string browser_download_url { get; set; }
        }

        public class VersionInfoData
        {
            public string Version { get; set; }
        }
    }
}