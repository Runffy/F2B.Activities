using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using F2B.Browser.Chromium.Cdp.Inspector.Helpers;
using MediaColor = System.Windows.Media.Color;

namespace F2B.Browser.Chromium.Cdp.Inspector.Overlays
{
    /// <summary>
    /// Lightweight bubble shown near the cursor during CDP indicate pick.
    /// </summary>
    internal sealed class CursorBubbleOverlay : Window
    {
        private const int OffsetX = 18;
        private const int OffsetY = 22;
        private readonly TextBlock _messageText;

        public CursorBubbleOverlay()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            IsHitTestVisible = false;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;

            _messageText = new TextBlock
            {
                Text = string.Empty,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 280,
                FontFamily = new FontFamily("Segoe UI")
            };

            Content = new Border
            {
                Background = new SolidColorBrush(MediaColor.FromArgb(220, 30, 30, 30)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 6, 10, 6),
                Child = _messageText
            };
        }

        public void ShowNearCursor(string message)
        {
            _messageText.Text = message ?? string.Empty;
            PositionNearCursor();

            if (!IsVisible)
            {
                ShowActivated = false;
                Show();
            }

            EnsureTopmostNoActivate();
        }

        public new void Hide()
        {
            if (IsVisible)
                base.Hide();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            EnableClickThroughNoActivate();
        }

        private void PositionNearCursor()
        {
            var cursor = System.Windows.Forms.Cursor.Position;
            Left = cursor.X + OffsetX;
            Top = cursor.Y + OffsetY;
        }

        private void EnsureTopmostNoActivate()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            NativeMethods.SetWindowPos(
                hwnd,
                NativeMethods.HWND_TOPMOST,
                (int)Left,
                (int)Top,
                (int)ActualWidth,
                (int)ActualHeight,
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
        }

        private void EnableClickThroughNoActivate()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            const int wsExTransparent = 0x00000020;
            const int wsExNoActivate = 0x08000000;
            const int wsExToolWindow = 0x00000080;
            const int gwlExstyle = -20;

            var style = GetWindowLongPtr(hwnd, gwlExstyle);
            SetWindowLongPtr(hwnd, gwlExstyle, new IntPtr(style.ToInt64() | wsExTransparent | wsExNoActivate | wsExToolWindow));
        }

        private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(hWnd, nIndex)
                : GetWindowLongPtr32(hWnd, nIndex);
        }

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            return IntPtr.Size == 8
                ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
                : SetWindowLongPtr32(hWnd, nIndex, dwNewLong);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    }
}
