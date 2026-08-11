using System;
using System.Windows.Forms;
using F2B.Forms.Designer;

namespace F2B.Forms.Viewer
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string initialPath = args != null && args.Length > 0 ? args[0] : null;
            Application.Run(new MainForm(isViewer: true, initialPath: initialPath));
        }
    }
}
