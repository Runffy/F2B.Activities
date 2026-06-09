using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using FlaUI.Inspector.Helpers;

namespace FlaUI.Inspector.Services
{
    public sealed class GlobalInputHook : IDisposable
    {
        private NativeMethods.LowLevelHookProc _mouseProc;
        private IntPtr _mouseHook = IntPtr.Zero;
        private bool _disposed;
        private bool _clickHandled;

        public event Action<int, int> MouseMoved;
        public event Action<int, int> MouseButtonDown;

        /// <summary>
        /// Indicate 模式下吞掉左键按下/释放，避免点击传递到目标应用导致菜单收起。
        /// </summary>
        public bool ConsumeMouseClick { get; set; }

        public void Start()
        {
            if (_mouseHook != IntPtr.Zero)
                return;

            _mouseProc = MouseHookCallback;
            var moduleHandle = Marshal.GetHINSTANCE(typeof(GlobalInputHook).Module);
            _mouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseProc, moduleHandle, 0);
            if (_mouseHook == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to install mouse hook.");
        }

        public void Stop()
        {
            if (_mouseHook != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_mouseHook);
                _mouseHook = IntPtr.Zero;
            }

            _clickHandled = false;
            ConsumeMouseClick = false;
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                var message = wParam.ToInt32();

                if (message == NativeMethods.WM_MOUSEMOVE)
                {
                    MouseMoved?.Invoke(hookStruct.pt.X, hookStruct.pt.Y);
                }
                else if (message == NativeMethods.WM_LBUTTONDOWN)
                {
                    if (ConsumeMouseClick)
                    {
                        _clickHandled = true;
                        MouseButtonDown?.Invoke(hookStruct.pt.X, hookStruct.pt.Y);
                        return (IntPtr)1;
                    }
                }
                else if (message == NativeMethods.WM_LBUTTONUP)
                {
                    if (ConsumeMouseClick)
                    {
                        var handled = _clickHandled;
                        _clickHandled = false;
                        if (handled)
                            return (IntPtr)1;
                    }
                }
            }

            return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Stop();
            _disposed = true;
        }
    }
}
