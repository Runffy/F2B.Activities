using System;
using System.Text;
using F2B.Browser.Chromium.Cdp.Inspector.Helpers;

namespace F2B.Browser.Chromium.Cdp.Inspector.Services
{
    /// <summary>
    /// Resolves the Chromium browser under a screen point for CDP indicate.
    /// </summary>
    internal static class BrowserFromPoint
    {
        public enum HitKind
        {
            None,
            /// <summary>Chrome/Edge window without a usable remote-debugging-port.</summary>
            ChromiumWithoutDebugPort,
            /// <summary>Chrome/Edge with a live CDP port.</summary>
            DebuggableBrowser
        }

        public sealed class HitResult
        {
            public HitKind Kind { get; set; }

            public CdpDiscoveredBrowser Browser { get; set; }

            public IntPtr WindowHandle { get; set; }

            public int ProcessId { get; set; }
        }

        /// <summary>
        /// Fast HWND/PID only — no WMI/CDP. Used for sticky caching in Indicate.
        /// </summary>
        public static HitResult ResolveWindowProcess(int screenX, int screenY)
        {
            var point = new NativeMethods.POINT { X = screenX, Y = screenY };
            var hwnd = NativeMethods.WindowFromPoint(point);
            if (hwnd == IntPtr.Zero)
            {
                return new HitResult { Kind = HitKind.None };
            }

            var root = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);
            if (root != IntPtr.Zero)
            {
                hwnd = root;
            }

            uint processId;
            NativeMethods.GetWindowThreadProcessId(hwnd, out processId);
            return new HitResult
            {
                Kind = HitKind.None,
                WindowHandle = hwnd,
                ProcessId = (int)processId
            };
        }

        public static HitResult Resolve(int screenX, int screenY)
        {
            var point = new NativeMethods.POINT { X = screenX, Y = screenY };
            var hwnd = NativeMethods.WindowFromPoint(point);
            if (hwnd == IntPtr.Zero)
            {
                return new HitResult { Kind = HitKind.None };
            }

            var root = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);
            if (root != IntPtr.Zero)
            {
                hwnd = root;
            }

            uint processId;
            NativeMethods.GetWindowThreadProcessId(hwnd, out processId);
            if (processId == 0)
            {
                return new HitResult { Kind = HitKind.None, WindowHandle = hwnd };
            }

            var pid = (int)processId;
            var isChromium = CdpBrowserDiscovery.IsChromiumBrowserProcess(pid) || LooksLikeChromiumWindow(hwnd);
            if (!isChromium)
            {
                return new HitResult { Kind = HitKind.None, WindowHandle = hwnd, ProcessId = pid };
            }

            if (CdpBrowserDiscovery.TryResolveFromProcessId(pid, out var browser))
            {
                return new HitResult
                {
                    Kind = HitKind.DebuggableBrowser,
                    Browser = browser,
                    WindowHandle = hwnd,
                    ProcessId = pid
                };
            }

            return new HitResult
            {
                Kind = HitKind.ChromiumWithoutDebugPort,
                WindowHandle = hwnd,
                ProcessId = pid
            };
        }

        private static bool LooksLikeChromiumWindow(IntPtr hwnd)
        {
            var className = new StringBuilder(256);
            if (NativeMethods.GetClassName(hwnd, className, className.Capacity) <= 0)
            {
                return false;
            }

            var name = className.ToString();
            return name.IndexOf("Chrome_WidgetWin", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Chrome_RenderWidgetHostHWND", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
