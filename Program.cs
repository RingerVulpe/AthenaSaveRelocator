using System;
using System.Windows.Forms;

namespace AthenaSaveRelocator
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Prevent multiple instances of the application
            if (System.Diagnostics.Process.GetProcessesByName(System.Diagnostics.Process.GetCurrentProcess().ProcessName).Length > 1)
            {
                MessageBox.Show("Another instance of the application is already running.", "Athena Save Relocator", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new AthenaSaveRelocatorForm());
        }
    }
}
