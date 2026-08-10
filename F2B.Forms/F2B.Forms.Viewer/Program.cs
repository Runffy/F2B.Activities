using System;
using System.Windows.Forms;
using F2B.Forms.Designer;

namespace F2B.Forms.Viewer
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(isViewer: true));
        }
    }
}
