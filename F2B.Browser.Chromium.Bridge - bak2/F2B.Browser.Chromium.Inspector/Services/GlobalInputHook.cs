using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using F2B.Browser.Chromium.Inspector.Helpers;

namespace F2B.Browser.Chromium.Inspector.Services
{
    internal sealed class GlobalInputHook : IDisposable
    {
        private NativeMethods.LowLevelHookProc _mouseProc;
        private IntPtr _mouseHook = IntPtr.Zero;
        private bool _disposed;
        private bool _clickHandled;

        public event Action<int, int> MouseMoved;
        public event Action<int, int> MouseButtonDown;

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
