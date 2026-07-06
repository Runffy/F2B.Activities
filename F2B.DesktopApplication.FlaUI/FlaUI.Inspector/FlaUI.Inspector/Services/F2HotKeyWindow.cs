using System;
using System.Drawing;
using System.Windows.Forms;
using FlaUI.Inspector.Helpers;

namespace FlaUI.Inspector.Services
{
    /// <summary>
    /// 用 RegisterHotKey 在系统级注册 F2，不依赖低层键盘钩子，避免 F2 泄漏到 VS 等前台窗口。
    /// </summary>
    internal sealed class F2HotKeyWindow : Form
    {
        private const int HotKeyId = 0xF201;

        public event Action F2Pressed;

        public F2HotKeyWindow()
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
            return NativeMethods.RegisterHotKey(
                Handle,
                HotKeyId,
                NativeMethods.MOD_NOREPEAT,
                NativeMethods.VK_F2);
        }

        public void Unregister()
        {
            if (IsHandleCreated)
                NativeMethods.UnregisterHotKey(Handle, HotKeyId);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_HOTKEY && m.WParam.ToInt32() == HotKeyId)
            {
                F2Pressed?.Invoke();
                return;
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
