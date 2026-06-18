using System;
using System.Windows;
using System.Windows.Interop;
using F2B.Browser.Chromium.Inspector.Helpers;

namespace F2B.Browser.Chromium.Inspector.Services
{
    /// <summary>
    /// 在 WPF 主窗口句柄上注册 F2 / Esc 全局热键（对齐 FlaUI 的 RegisterHotKey 方案，但挂在 WPF HwndSource 上以确保消息送达）。
    /// </summary>
    internal sealed class IndicateHotKeyHandler : IDisposable
    {
        private const int F2HotKeyId = 0xF201;
        private const int EscHotKeyId = 0xF202;

        private readonly Window _owner;
        private HwndSource _hwndSource;
        private IntPtr _hwnd;
        private bool _disposed;

        public event Action F2Pressed;
        public event Action EscapePressed;

        public IndicateHotKeyHandler(Window owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public bool TryRegister()
        {
            EnsureHook();
            Unregister();

            var f2Ok = NativeMethods.RegisterHotKey(
                _hwnd,
                F2HotKeyId,
                NativeMethods.MOD_NOREPEAT,
                NativeMethods.VK_F2);
            var escOk = NativeMethods.RegisterHotKey(
                _hwnd,
                EscHotKeyId,
                NativeMethods.MOD_NOREPEAT,
                NativeMethods.VK_ESCAPE);

            return f2Ok && escOk;
        }

        public void Unregister()
        {
            if (_hwnd == IntPtr.Zero)
                return;

            NativeMethods.UnregisterHotKey(_hwnd, F2HotKeyId);
            NativeMethods.UnregisterHotKey(_hwnd, EscHotKeyId);
        }

        private void EnsureHook()
        {
            if (_hwndSource != null)
                return;

            _hwnd = new WindowInteropHelper(_owner).Handle;
            if (_hwnd == IntPtr.Zero)
                throw new InvalidOperationException("Indicate hotkeys require a visible main window handle.");

            _hwndSource = HwndSource.FromHwnd(_hwnd);
            if (_hwndSource == null)
                throw new InvalidOperationException("Failed to attach HwndSource for indicate hotkeys.");

            _hwndSource.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != NativeMethods.WM_HOTKEY)
                return IntPtr.Zero;

            var id = wParam.ToInt32();
            if (id == F2HotKeyId)
            {
                F2Pressed?.Invoke();
                handled = true;
            }
            else if (id == EscHotKeyId)
            {
                EscapePressed?.Invoke();
                handled = true;
            }

            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Unregister();

            if (_hwndSource != null)
            {
                _hwndSource.RemoveHook(WndProc);
                _hwndSource = null;
            }

            _disposed = true;
        }
    }
}
