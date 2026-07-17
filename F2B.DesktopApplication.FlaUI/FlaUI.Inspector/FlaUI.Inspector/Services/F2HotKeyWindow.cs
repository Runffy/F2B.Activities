using System;
using System.Drawing;
using System.Windows.Forms;
using FlaUI.Inspector.Helpers;

namespace FlaUI.Inspector.Services
{
    /// <summary>
    /// 用 RegisterHotKey 在系统级注册 F2 / Esc，不依赖低层键盘钩子，避免热键泄漏到 VS 等前台窗口。
    /// </summary>
    internal sealed class F2HotKeyWindow : Form
    {
        private const int F2HotKeyId = 0xF201;
        private const int EscHotKeyId = 0xF202;

        public event Action F2Pressed;
        public event Action EscapePressed;

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
            var f2Ok = NativeMethods.RegisterHotKey(
                Handle,
                F2HotKeyId,
                NativeMethods.MOD_NOREPEAT,
                NativeMethods.VK_F2);
            var escOk = NativeMethods.RegisterHotKey(
                Handle,
                EscHotKeyId,
                NativeMethods.MOD_NOREPEAT,
                NativeMethods.VK_ESCAPE);
            return f2Ok && escOk;
        }

        /// <summary>
        /// F2 暂停倒计时期间只注销 F2，保留 Esc 以便取消 Indicate。
        /// </summary>
        public void UnregisterF2()
        {
            if (IsHandleCreated)
                NativeMethods.UnregisterHotKey(Handle, F2HotKeyId);
        }

        public void Unregister()
        {
            if (!IsHandleCreated)
                return;

            NativeMethods.UnregisterHotKey(Handle, F2HotKeyId);
            NativeMethods.UnregisterHotKey(Handle, EscHotKeyId);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_HOTKEY)
            {
                var id = m.WParam.ToInt32();
                if (id == F2HotKeyId)
                {
                    F2Pressed?.Invoke();
                    return;
                }

                if (id == EscHotKeyId)
                {
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
