using System;
using System.Windows.Forms;

namespace AthenaSaveRelocator
{
    internal static class Program
    {

        [STAThread]
        static void Main(string[] args)
        {

            // Prevent multiple instances of the application
            if (System.Diagnostics.Process.GetProcessesByName(System.Diagnostics.Process.GetCurrentProcess().ProcessName).Length > 1)
            {
                MessageBox.Show("Another instance of the application is already running.", "Athena Save Relocator", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Check if the application was started with the --updated argument
            if (args.Contains("--updated"))
            {
                MessageBox.Show(
                    "The application has been successfully updated to the latest version.",
                    "Update Successful",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                Application.Run(new AthenaSaveRelocatorForm(true));


            }
            else
            {
                Application.Run(new AthenaSaveRelocatorForm(false));
            }

        }
    }
}
