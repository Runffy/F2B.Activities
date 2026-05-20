using System;
using System.Runtime.InteropServices;
using System.Threading;
using F2B.Browser.IExplore;

namespace F2B.Browser.IExplore.Native
{
    internal static class MouseInputHelper
    {
        private const uint MouseeventfLeftDown = 0x0002;
        private const uint MouseeventfLeftUp = 0x0004;
        private const uint MouseeventfRightDown = 0x0008;
        private const uint MouseeventfRightUp = 0x0010;
        private const uint MouseeventfMiddleDown = 0x0020;
        private const uint MouseeventfMiddleUp = 0x0040;

        [StructLayout(LayoutKind.Sequential)]
        private struct Point
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref Point lpPoint);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        /// <summary>
        /// Physical click: post mouse messages to the IE server HWND (reliable), then move the system cursor.
        /// </summary>
        public static void ClickAtClientPoint(
            IntPtr topLevelHwnd,
            IntPtr ieServerHwnd,
            int clientX,
            int clientY,
            MouseButton button)
        {
            if (ieServerHwnd == IntPtr.Zero)
                throw new ArgumentException("IE server HWND is required.", nameof(ieServerHwnd));

            Win32Native.SetForegroundWindow(topLevelHwnd);
            Thread.Sleep(80);

            var lParam = Win32Native.MakeLParam(clientX, clientY);
            SendButtonMessages(ieServerHwnd, button, lParam);

            Thread.Sleep(40);
            MoveCursorAndClick(topLevelHwnd, ieServerHwnd, clientX, clientY, button);
        }

        /// <summary>Two physical clicks at the same point (double-click).</summary>
        public static void DoubleClickAtClientPoint(
            IntPtr topLevelHwnd,
            IntPtr ieServerHwnd,
            int clientX,
            int clientY,
            MouseButton button,
            int intervalMs = 100)
        {
            if (intervalMs < 0)
                intervalMs = 0;

            ClickAtClientPoint(topLevelHwnd, ieServerHwnd, clientX, clientY, button);
            Thread.Sleep(intervalMs);
            ClickAtClientPoint(topLevelHwnd, ieServerHwnd, clientX, clientY, button);
        }

        private static void SendButtonMessages(IntPtr ieServerHwnd, MouseButton button, IntPtr lParam)
        {
            int downMsg;
            int upMsg;
            IntPtr downWParam;

            switch (button)
            {
                case MouseButton.Right:
                    downMsg = Win32Native.WmRbuttonDown;
                    upMsg = Win32Native.WmRbuttonUp;
                    downWParam = (IntPtr)Win32Native.MkRbutton;
                    break;
                case MouseButton.Middle:
                    downMsg = Win32Native.WmMbuttonDown;
                    upMsg = Win32Native.WmMbuttonUp;
                    downWParam = (IntPtr)Win32Native.MkMbutton;
                    break;
                default:
                    downMsg = Win32Native.WmLbuttonDown;
                    upMsg = Win32Native.WmLbuttonUp;
                    downWParam = (IntPtr)Win32Native.MkLbutton;
                    break;
            }

            Win32Native.SendMessage(ieServerHwnd, downMsg, downWParam, lParam);
            Thread.Sleep(30);
            Win32Native.SendMessage(ieServerHwnd, upMsg, IntPtr.Zero, lParam);
        }

        private static void MoveCursorAndClick(
            IntPtr topLevelHwnd,
            IntPtr ieServerHwnd,
            int clientX,
            int clientY,
            MouseButton button)
        {
            var pt = new Point { X = clientX, Y = clientY };
            if (!ClientToScreen(ieServerHwnd, ref pt))
                return;

            SetCursorPos(pt.X, pt.Y);
            Thread.Sleep(30);

            uint down;
            uint up;
            switch (button)
            {
                case MouseButton.Middle:
                    down = MouseeventfMiddleDown;
                    up = MouseeventfMiddleUp;
                    break;
                case MouseButton.Right:
                    down = MouseeventfRightDown;
                    up = MouseeventfRightUp;
                    break;
                default:
                    down = MouseeventfLeftDown;
                    up = MouseeventfLeftUp;
                    break;
            }

            mouse_event(down, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(20);
            mouse_event(up, 0, 0, 0, UIntPtr.Zero);
        }
    }
}
