using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using IE.Inspector.Helpers;

namespace IE.Inspector.Services
{
    public sealed class GlobalInputHook : IDisposable
    {
        private NativeMethods.LowLevelHookProc _mouseProc;
        private NativeMethods.LowLevelHookProc _keyboardProc;
        private IntPtr _mouseHook = IntPtr.Zero;
        private IntPtr _keyboardHook = IntPtr.Zero;
        private bool _disposed;
        private bool _clickHandled;

        public event Action<int, int> MouseMoved;
        public event Action<int, int> MouseButtonDown;
        public event Action EscapePressed;

        public bool ConsumeMouseClick { get; set; }

        public void Start(bool captureEscape = true)
        {
            if (_mouseHook == IntPtr.Zero)
            {
                _mouseProc = MouseHookCallback;
                var moduleHandle = Marshal.GetHINSTANCE(typeof(GlobalInputHook).Module);
                _mouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseProc, moduleHandle, 0);
                if (_mouseHook == IntPtr.Zero)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to install mouse hook.");
            }

            if (captureEscape && _keyboardHook == IntPtr.Zero)
            {
                _keyboardProc = KeyboardHookCallback;
                var moduleHandle = Marshal.GetHINSTANCE(typeof(GlobalInputHook).Module);
                _keyboardHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _keyboardProc, moduleHandle, 0);
                if (_keyboardHook == IntPtr.Zero)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to install keyboard hook.");
            }
        }

        public void Stop()
        {
            if (_mouseHook != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_mouseHook);
                _mouseHook = IntPtr.Zero;
            }

            if (_keyboardHook != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_keyboardHook);
                _keyboardHook = IntPtr.Zero;
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

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var message = wParam.ToInt32();
                if (message == NativeMethods.WM_KEYDOWN || message == NativeMethods.WM_SYSKEYDOWN)
                {
                    var hookStruct = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
                    if (hookStruct.vkCode == NativeMethods.VK_ESCAPE)
                    {
                        EscapePressed?.Invoke();
                        return (IntPtr)1;
                    }
                }
            }

            return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
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
