using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace IE.Inspector.Overlays
{
    public static class HighlightOverlay
    {
        public static async Task ShowAsync(Rectangle bounds, int durationMilliseconds = 3000)
        {
            if (bounds.IsEmpty)
                return;

            using (var form = new HighlightForm(bounds))
            {
                form.Show();
                await Task.Delay(durationMilliseconds).ConfigureAwait(false);
                form.Close();
            }
        }

        private sealed class HighlightForm : Form
        {
            public HighlightForm(Rectangle bounds)
            {
                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;
                StartPosition = FormStartPosition.Manual;
                BackColor = Color.Magenta;
                TransparencyKey = Color.Magenta;
                TopMost = true;
                Bounds = bounds;
            }

            protected override CreateParams CreateParams
            {
                get
                {
                    var cp = base.CreateParams;
                    cp.ExStyle |= 0x00000080;
                    cp.ExStyle |= 0x08000000;
                    cp.ExStyle |= 0x00000008;
                    cp.ExStyle |= 0x00000020;
                    return cp;
                }
            }

            protected override bool ShowWithoutActivation => true;

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                using (var pen = new Pen(Color.Red, 3))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                }
            }

            protected override void CreateHandle()
            {
                base.CreateHandle();
                SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0010 | 0x0002 | 0x0001);
            }

            [DllImport("user32.dll")]
            private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
        }
    }
}
