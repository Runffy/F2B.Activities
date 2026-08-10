using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    /// <summary>
    /// Soft-activates a Chromium OS window (bring to front / focus) without HWND_TOPMOST,
    /// so other apps can later cover it normally.
    /// </summary>
    internal static class CdpNativeWindowActivator
    {
        private const int SwRestore = 9;
        private const int SwShow = 5;
        private const int GaRoot = 2;

        internal static void TryBringToFront(int rootProcessId, string preferredTitle)
        {
            if (rootProcessId <= 0)
            {
                return;
            }

            try
            {
                var processIds = CollectProcessTree(rootProcessId);
                IntPtr hwnd = FindBestWindow(processIds, preferredTitle);
                if (hwnd == IntPtr.Zero)
                {
                    return;
                }

                SoftActivate(hwnd);
            }
            catch
            {
                // Best effort — window state change already applied via CDP.
            }
        }

        private static HashSet<int> CollectProcessTree(int rootProcessId)
        {
            var result = new HashSet<int> { rootProcessId };
            var queue = new Queue<int>();
            queue.Enqueue(rootProcessId);

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                IEnumerable<int> children;
                try
                {
                    children = ProcessCommandLine.GetChildProcessIds(current);
                }
                catch
                {
                    continue;
                }

                if (children == null)
                {
                    continue;
                }

                foreach (int child in children)
                {
                    if (child > 0 && result.Add(child))
                    {
                        queue.Enqueue(child);
                    }
                }
            }

            return result;
        }

        private static IntPtr FindBestWindow(HashSet<int> processIds, string preferredTitle)
        {
            var candidates = new List<WindowCandidate>();
            EnumWindows(
                (hwnd, lParam) =>
                {
                    if (!IsWindow(hwnd) || !IsWindowVisible(hwnd))
                    {
                        return true;
                    }

                    // Prefer top-level frames only.
                    if (GetAncestor(hwnd, GaRoot) != hwnd)
                    {
                        return true;
                    }

                    uint windowPid;
                    GetWindowThreadProcessId(hwnd, out windowPid);
                    if (windowPid == 0 || !processIds.Contains((int)windowPid))
                    {
                        return true;
                    }

                    string className = GetWindowClassName(hwnd);
                    // Chromium / Electron style browser frame.
                    if (className != null
                        && className.IndexOf("Chrome_WidgetWin", StringComparison.OrdinalIgnoreCase) < 0
                        && className.IndexOf("Chrome_WindowImpl", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        // Still allow generic top-level windows from the process tree as fallback.
                    }

                    string title = GetWindowTitle(hwnd);
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        return true;
                    }

                    RECT rect;
                    int area = 0;
                    if (GetWindowRect(hwnd, out rect))
                    {
                        area = Math.Max(0, rect.Right - rect.Left) * Math.Max(0, rect.Bottom - rect.Top);
                    }

                    bool titleMatch = !string.IsNullOrWhiteSpace(preferredTitle)
                        && title.IndexOf(preferredTitle.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;

                    bool isChromeFrame = className != null
                        && className.IndexOf("Chrome_WidgetWin", StringComparison.OrdinalIgnoreCase) >= 0;

                    candidates.Add(new WindowCandidate
                    {
                        Handle = hwnd,
                        Area = area,
                        TitleMatch = titleMatch,
                        IsChromeFrame = isChromeFrame
                    });

                    return true;
                },
                IntPtr.Zero);

            if (candidates.Count == 0)
            {
                return IntPtr.Zero;
            }

            candidates.Sort((a, b) =>
            {
                int cmp = b.TitleMatch.CompareTo(a.TitleMatch);
                if (cmp != 0)
                {
                    return cmp;
                }

                cmp = b.IsChromeFrame.CompareTo(a.IsChromeFrame);
                if (cmp != 0)
                {
                    return cmp;
                }

                return b.Area.CompareTo(a.Area);
            });

            return candidates[0].Handle;
        }

        private static void SoftActivate(IntPtr hwnd)
        {
            if (IsIconic(hwnd))
            {
                ShowWindow(hwnd, SwRestore);
            }
            else
            {
                ShowWindow(hwnd, SwShow);
            }

            IntPtr foreground = GetForegroundWindow();
            uint foregroundThread = foreground == IntPtr.Zero
                ? 0
                : GetWindowThreadProcessId(foreground, out _);
            uint targetThread = GetWindowThreadProcessId(hwnd, out _);
            uint currentThread = GetCurrentThreadId();

            bool attachedForeground = false;
            bool attachedTarget = false;
            try
            {
                if (foregroundThread != 0 && foregroundThread != currentThread)
                {
                    attachedForeground = AttachThreadInput(currentThread, foregroundThread, true);
                }

                if (targetThread != 0 && targetThread != currentThread)
                {
                    attachedTarget = AttachThreadInput(currentThread, targetThread, true);
                }

                BringWindowToTop(hwnd);
                SetForegroundWindow(hwnd);
            }
            finally
            {
                if (attachedTarget)
                {
                    AttachThreadInput(currentThread, targetThread, false);
                }

                if (attachedForeground)
                {
                    AttachThreadInput(currentThread, foregroundThread, false);
                }
            }
        }

        private static string GetWindowTitle(IntPtr hwnd)
        {
            int length = GetWindowTextLength(hwnd);
            if (length <= 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(length + 1);
            GetWindowText(hwnd, builder, builder.Capacity);
            return builder.ToString();
        }

        private static string GetWindowClassName(IntPtr hwnd)
        {
            var builder = new StringBuilder(256);
            return GetClassName(hwnd, builder, builder.Capacity) > 0
                ? builder.ToString()
                : string.Empty;
        }

        private sealed class WindowCandidate
        {
            public IntPtr Handle { get; set; }
            public int Area { get; set; }
            public bool TitleMatch { get; set; }
            public bool IsChromeFrame { get; set; }
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}
