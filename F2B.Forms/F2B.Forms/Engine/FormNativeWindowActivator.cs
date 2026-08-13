using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace F2B.Forms.Engine
{
    /// <summary>
    /// Soft-activates a WinForms window: brings it to the front once without leaving
    /// permanent TopMost, so other activated windows can cover it again.
    /// </summary>
    internal static class FormNativeWindowActivator
    {
        private const int SwRestore = 9;
        private const int SwShow = 5;

        internal static void SoftBringToFront(Form form)
        {
            if (form == null || form.IsDisposed)
            {
                return;
            }

            if (!form.Visible)
            {
                form.Show();
            }

            if (form.WindowState == FormWindowState.Minimized)
            {
                form.WindowState = FormWindowState.Normal;
            }

            // Temporary TopMost flash — NOT left on (non-sticky / 非霸占).
            bool previousTopMost = form.TopMost;
            try
            {
                form.TopMost = true;
                form.BringToFront();
                form.Activate();
            }
            finally
            {
                form.TopMost = previousTopMost;
            }

            // Ensure TopMost is off unless the form was already permanently TopMost.
            if (!previousTopMost)
            {
                form.TopMost = false;
            }

            SoftActivateHwnd(form.Handle);
        }

        private static void SoftActivateHwnd(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd))
            {
                return;
            }

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

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
