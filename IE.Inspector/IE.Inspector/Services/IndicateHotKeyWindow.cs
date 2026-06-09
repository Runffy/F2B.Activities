using System;
using System.Drawing;
using System.Windows.Forms;
using IE.Inspector.Helpers;

namespace IE.Inspector.Services
{
    internal sealed class IndicateHotKeyWindow : Form
    {
        private const int F2HotKeyId = 0xF201;
        private const int EscapeHotKeyId = 0xF202;

        public event Action F2Pressed;
        public event Action EscapePressed;

        public IndicateHotKeyWindow()
        {
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
            Opacity = 0;
            Size = new Size(1, 1);
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-32000, -32000);
        }

        protected override void SetVisibleCore(bool value)
        {
            if (!IsHandleCreated)
                CreateHandle();
            base.SetVisibleCore(false);
        }

        public bool TryRegister()
        {
            if (!IsHandleCreated)
                CreateHandle();

            Unregister();

            var f2Ok = NativeMethods.RegisterHotKey(Handle, F2HotKeyId, NativeMethods.MOD_NOREPEAT, NativeMethods.VK_F2);
            var escOk = NativeMethods.RegisterHotKey(Handle, EscapeHotKeyId, NativeMethods.MOD_NOREPEAT, NativeMethods.VK_ESCAPE);
            return f2Ok && escOk;
        }

        public void Unregister()
        {
            if (!IsHandleCreated)
                return;

            NativeMethods.UnregisterHotKey(Handle, F2HotKeyId);
            NativeMethods.UnregisterHotKey(Handle, EscapeHotKeyId);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_HOTKEY)
            {
                switch (m.WParam.ToInt32())
                {
                    case F2HotKeyId:
                        F2Pressed?.Invoke();
                        return;
                    case EscapeHotKeyId:
                        EscapePressed?.Invoke();
                        return;
                }
            }

            base.WndProc(ref m);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                Unregister();

            base.Dispose(disposing);
        }
    }
}
