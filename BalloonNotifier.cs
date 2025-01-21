using System;
using System.Windows.Forms;

namespace AthenaSaveRelocator
{
    /// <summary>
    /// Manages showing balloon tooltips for the NotifyIcon.
    /// </summary>
    internal class BalloonNotifier
    {
        /// <summary>
        /// Displays a balloon notification with the given title, text, and timeout.
        /// </summary>
        public void ShowBalloonNotification(
            NotifyIcon trayIcon,
            string title,
            string text,
            int timeoutMs = 3000)
        {
            // Force the tray icon to re-initialize so Windows doesn't ignore repeated balloons
            trayIcon.Visible = false;
            trayIcon.Visible = true;

            // Append a short timestamp to ensure uniqueness
            trayIcon.BalloonTipTitle = title;
            trayIcon.BalloonTipText = $"{text} ({DateTime.Now:HH:mm:ss})";
            trayIcon.BalloonTipIcon = ToolTipIcon.Info;

            trayIcon.ShowBalloonTip(timeoutMs);
        }
    }
}
