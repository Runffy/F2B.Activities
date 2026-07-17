using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using FlaUI.Inspector.Helpers;

namespace FlaUI.Inspector.Services
{
    public sealed class GlobalInputHook : IDisposable
    {
        private NativeMethods.LowLevelHookProc _mouseProc;
        private IntPtr _mouseHook = IntPtr.Zero;
        private bool _disposed;
        private int _pendingButtonUpConsumes;

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

            Interlocked.Exchange(ref _pendingButtonUpConsumes, 0);
            ConsumeMouseClick = false;
        }

        /// <summary>
        /// 在后台线程等待配对的 LBUTTONUP 被吞掉。勿在安装钩子的 UI 线程上调用（Sleep 会阻塞消息泵）。
        /// </summary>
        public void WaitForPendingButtonUp(int timeoutMs)
        {
            if (_mouseHook == IntPtr.Zero || Volatile.Read(ref _pendingButtonUpConsumes) <= 0)
                return;

            var deadline = Environment.TickCount + timeoutMs;
            while (Volatile.Read(ref _pendingButtonUpConsumes) > 0)
            {
                var remaining = deadline - Environment.TickCount;
                if (remaining <= 0)
                    break;

                Thread.Sleep(Math.Min(10, remaining));
            }
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
                        Interlocked.Increment(ref _pendingButtonUpConsumes);
                        // 订阅方必须快速返回；耗时工作应异步排队，否则低层钩子会超时导致点击泄漏。
                        MouseButtonDown?.Invoke(hookStruct.pt.X, hookStruct.pt.Y);
                        return (IntPtr)1;
                    }
                }
                else if (message == NativeMethods.WM_LBUTTONUP)
                {
                    // ConsumeMouseClick 关闭后仍吞掉配对的 UP（例如 Capture 完成后即将卸钩）。
                    if (ConsumeMouseClick || Volatile.Read(ref _pendingButtonUpConsumes) > 0)
                    {
                        if (Volatile.Read(ref _pendingButtonUpConsumes) > 0)
                            Interlocked.Decrement(ref _pendingButtonUpConsumes);
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
