using System;
using F2B.Browser.IExplore.Native;

namespace F2B.Browser.IExplore.Com
{
    /// <summary>Distinguish real IE browser frames from embedded Trident hosts (OCX, AtlAx, etc.).</summary>
    internal static class IeHostWindow
    {
        public const string IeFrameClass = "IEFrame";

        public static IntPtr ResolveBrowserFrameHwnd(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !Win32Native.IsWindow(hwnd))
                return IntPtr.Zero;

            var current = hwnd;
            for (int i = 0; i < 32 && current != IntPtr.Zero; i++)
            {
                var cls = Win32Native.GetClassNameString(current);
                if (cls.Equals(IeFrameClass, StringComparison.OrdinalIgnoreCase))
                    return current;

                current = Win32Native.GetParent(current);
            }

            return IntPtr.Zero;
        }

        public static bool IsInternetExplorerBrowser(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
                return false;

            var frame = ResolveBrowserFrameHwnd(hwnd);
            if (frame == IntPtr.Zero)
                return false;

            var shell = ShDocVwHelper.FindByHwnd((int)frame.ToInt64());
            if (shell != null && !string.IsNullOrEmpty(shell.HostPath))
                return shell.IsInternetExplorer;

            // No Shell entry — accept only if top-level class is IEFrame.
            var top = Win32Native.GetRootHwnd(hwnd);
            return Win32Native.GetClassNameString(top)
                .Equals(IeFrameClass, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsEmbeddedHostClass(string className)
        {
            if (string.IsNullOrEmpty(className))
                return false;

            return className.StartsWith("AtlAxWin", StringComparison.OrdinalIgnoreCase)
                || className.StartsWith("Shell Embedding", StringComparison.OrdinalIgnoreCase)
                || className.IndexOf("WebView", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
