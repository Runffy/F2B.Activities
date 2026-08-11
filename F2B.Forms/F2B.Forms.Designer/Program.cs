using System;
using System.Windows.Forms;

namespace F2B.Forms.Designer
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string initialPath = args != null && args.Length > 0 ? args[0] : null;
            Application.Run(new MainForm(isViewer: false, initialPath: initialPath));
        }
    }
}
