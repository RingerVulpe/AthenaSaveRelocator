using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AthenaSaveRelocator
{
    internal static class ForMyLove
    {
        public static void RunProgram()
        {
            // Short delay for a smooth launch experience

            ShowMessageSequence();
        }

        private static void ShowMessageSequence()
        {
            // First message box
            DialogResult firstChoice = MessageBox.Show(
                "Hey love, have you had some water today?",
                "A Gentle Reminder",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (firstChoice == DialogResult.No)
            {
                // If she says No, remind her to drink water
                MessageBox.Show("Please drink some water, my love. It's important to stay hydrated! 💙",
                    "Reminder", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                // If she says Yes, acknowledge it
                MessageBox.Show("Good! Proud of you. 😊", "Great!", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            // Second message box
            DialogResult secondChoice = MessageBox.Show(
                "Are you feeling okay today?",
                "Checking In",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (secondChoice == DialogResult.No)
            {
                // If she's not okay, offer comfort
                MessageBox.Show("I'm always here for you. Take a deep breath, and remember you're loved. 💕",
                    "Support", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                // If she says Yes
                MessageBox.Show("I'm so happy to hear that! 😊", "Wonderful!", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            // Third message box
            DialogResult thirdChoice = MessageBox.Show(
                "Do you want a hug? 🤗",
                "Hug Time",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (thirdChoice == DialogResult.Yes)
            {
                MessageBox.Show("Here's a big virtual hug! 🤗💖", "Hug Delivered!", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("No worries, but it's always here if you need it. 💙", "Always Here", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            // Final Message
            MessageBox.Show(
                "Remember to drink water, I love you, and have fun today! 💙",
                "Final Reminder",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }
    }
}
