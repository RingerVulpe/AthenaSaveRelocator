using System;
using System.Windows.Forms;

namespace AthenaSaveRelocator
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // EXACT same functionality as before:
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new AthenaSaveRelocatorForm());
        }
    }
}
