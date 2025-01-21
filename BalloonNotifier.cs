using System;
using System.Windows.Forms;

namespace AthenaSaveRelocator
{
    /// <summary>
    /// Manages showing notifications for the NotifyIcon with a fallback to a window.
    /// </summary>
    internal class BalloonNotifier
    {
        /// <summary>
        /// Displays a notification with the given title, text, and timeout.
        /// If the balloon notification does not appear, a fallback window is displayed.
        /// </summary>
        public void ShowNotification(NotifyIcon trayIcon, string title, string text, int timeoutMs = 3000)
        {
            try
            {
                // Dispose and recreate the NotifyIcon to ensure it shows
                trayIcon.Dispose();
                trayIcon = new NotifyIcon
                {
                    Icon = SystemIcons.Information,
                    Visible = true,
                    BalloonTipTitle = title,
                    BalloonTipText = $"{text} ({DateTime.Now:HH:mm:ss})",
                    BalloonTipIcon = ToolTipIcon.Info
                };

                // Show balloon tip
                trayIcon.ShowBalloonTip(timeoutMs);

                // Start a timer to check if the balloon was displayed
                System.Windows.Forms.Timer fallbackTimer = new System.Windows.Forms.Timer { Interval = timeoutMs + 500 }; // Add a small buffer
                fallbackTimer.Tick += (sender, args) =>
                {
                    fallbackTimer.Stop();
                    fallbackTimer.Dispose();

                    // Check if the notification was visible
                    if (!BalloonDisplayedRecently())
                    {
                        // Show fallback window
                        ShowFallbackWindow(title, text);
                    }
                };
                fallbackTimer.Start();
            }
            catch (Exception ex)
            {
                // In case of an exception, log it and show the fallback immediately
                Console.WriteLine($"Error showing notification: {ex.Message}");
                ShowFallbackWindow(title, text);
            }
        }

        /// <summary>
        /// Simulates a check to determine if the balloon notification was displayed.
        /// Note: Adjust as needed based on actual requirements.
        /// </summary>
        private bool BalloonDisplayedRecently()
        {
            // Windows API or NotifyIcon status check can be implemented here
            // This is a placeholder logic
            return false;
        }

        /// <summary>
        /// Shows a fallback window notification if the balloon notification fails.
        /// </summary>
        private void ShowFallbackWindow(string title, string text)
        {
            MessageBox.Show(text, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
