using System;
using System.Windows;
using System.Windows.Interop;

namespace FlaUI.Inspector.Helpers
{
    internal static class WindowRestoreHelper
    {
        /// <summary>
        /// Indicate 完成后恢复主窗口（可激活、置前）。此时数据已抓取完毕，无需再避免抢焦点。
        /// </summary>
        public static void RestoreAfterIndicateComplete(Window window)
        {
            if (window == null)
                return;

            window.ShowActivated = true;
            window.Visibility = Visibility.Visible;

            if (!window.IsLoaded)
            {
                window.SourceInitialized += OnSourceInitializedRestoreComplete;
                window.WindowState = WindowState.Normal;
                window.Show();
                return;
            }

            RestoreCompleteCore(window);
        }

        private static void OnSourceInitializedRestoreComplete(object sender, EventArgs e)
        {
            if (sender is Window window)
            {
                window.SourceInitialized -= OnSourceInitializedRestoreComplete;
                RestoreCompleteCore(window);
            }
        }

        private static void RestoreCompleteCore(Window window)
        {
            if (window.WindowState == WindowState.Minimized)
                window.WindowState = WindowState.Normal;

            window.Show();

            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd != IntPtr.Zero)
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);

            window.WindowState = WindowState.Normal;
            window.Activate();
            window.Focus();
        }
    }
}
