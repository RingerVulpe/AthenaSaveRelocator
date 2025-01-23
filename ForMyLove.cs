using System;
using System.Windows.Forms;

namespace AthenaSaveRelocator
{
    internal static class ForMyLove
    {
        public static void RunProgram()
        {
            Logger.Log("INFO: Entering ForMyLove.RunProgram()");
            try
            {
                ShowMessageSequence();
            }
            catch (Exception ex)
            {
                Logger.Log($"ERROR: Exception in ForMyLove.RunProgram: {ex.Message}");
            }
        }

        private static void ShowMessageSequence()
        {
            Logger.Log("INFO: Showing first message box (hydration check).");
            DialogResult firstChoice = MessageBox.Show(
                "Hey love, have you had some water today?",
                "A Gentle Reminder",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (firstChoice == DialogResult.No)
            {
                Logger.Log("INFO: User indicated no water yet, showing reminder.");
                MessageBox.Show(
                    "Please drink some water, my love. It's important to stay hydrated! 💙",
                    "Reminder",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            else
            {
                Logger.Log("INFO: User indicated they have had water.");
                MessageBox.Show(
                    "Good! Proud of you. 😊",
                    "Great!",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }

            Logger.Log("INFO: Showing second message box (feeling check).");
            DialogResult secondChoice = MessageBox.Show(
                "Are you feeling okay today?",
                "Checking In",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (secondChoice == DialogResult.No)
            {
                Logger.Log("INFO: User not feeling okay, offering comfort.");
                MessageBox.Show(
                    "I'm always here for you. Take a deep breath, and remember you're loved. 💕",
                    "Support",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            else
            {
                Logger.Log("INFO: User indicated they are feeling okay.");
                MessageBox.Show(
                    "I'm so happy to hear that! 😊",
                    "Wonderful!",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }

            Logger.Log("INFO: Showing third message box (hug offer).");
            DialogResult thirdChoice = MessageBox.Show(
                "Do you want a hug? 🤗",
                "Hug Time",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (thirdChoice == DialogResult.Yes)
            {
                Logger.Log("INFO: User wanted a hug.");
                MessageBox.Show(
                    "Here's a big virtual hug! 🤗💖",
                    "Hug Delivered!",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            else
            {
                Logger.Log("INFO: User declined a hug.");
                MessageBox.Show(
                    "No worries, but it's always here if you need it. 💙",
                    "Always Here",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }

            Logger.Log("INFO: Showing final message box (closing message).");
            MessageBox.Show(
                "Remember to drink water, I love you, and have fun today! 💙",
                "Final Reminder",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }
    }
}
