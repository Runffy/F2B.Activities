using System;
using System.Runtime.InteropServices;
using System.Text;

namespace F2B.Browser.IExplore.Native
{
    internal static class Win32Native
    {
        public const uint SmtoAbortIfHung = 0x0002;

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetParent(IntPtr hWnd);

        public const uint GaRoot = 2;

        [DllImport("user32.dll")]
        public static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern uint RegisterWindowMessage(string lpString);

        public const int WmLbuttonDown = 0x0201;
        public const int WmLbuttonUp = 0x0202;
        public const int WmRbuttonDown = 0x0204;
        public const int WmRbuttonUp = 0x0205;
        public const int WmMbuttonDown = 0x0207;
        public const int WmMbuttonUp = 0x0208;
        public const int MkLbutton = 0x0001;
        public const int MkRbutton = 0x0002;
        public const int MkMbutton = 0x0010;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SendMessageTimeout(
            IntPtr hWnd,
            uint msg,
            IntPtr wParam,
            IntPtr lParam,
            uint fuFlags,
            uint uTimeout,
            out IntPtr lpdwResult);

        [DllImport("oleacc.dll", PreserveSig = true)]
        public static extern int ObjectFromLresult(
            IntPtr lResult,
            ref Guid riid,
            uint wParam,
            [MarshalAs(UnmanagedType.Interface)] out object ppvObject);

        public static string GetClassNameString(IntPtr hwnd)
        {
            var sb = new StringBuilder(256);
            GetClassName(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        public static string GetWindowTextString(IntPtr hwnd)
        {
            var sb = new StringBuilder(512);
            GetWindowText(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        public static IntPtr GetRootHwnd(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
                return IntPtr.Zero;
            var root = GetAncestor(hwnd, GaRoot);
            return root != IntPtr.Zero ? root : hwnd;
        }

        public static IntPtr MakeLParam(int x, int y) =>
            (IntPtr)((y << 16) | (x & 0xFFFF));
    }
}
