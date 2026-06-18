using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using F2B.Browser.Chromium.Inspector.Helpers;
using MediaColor = System.Windows.Media.Color;

namespace F2B.Browser.Chromium.Inspector.Overlays
{
    internal sealed class CountdownOverlay : Window
    {
        private const int OverlaySize = 160;
        private readonly TextBlock _countText;

        public CountdownOverlay()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            IsHitTestVisible = false;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;
            Width = OverlaySize;
            Height = OverlaySize;

            _countText = new TextBlock
            {
                Text = "3",
                FontSize = 72,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Segoe UI")
            };

            Content = new Border
            {
                Background = new SolidColorBrush(MediaColor.FromArgb(220, 30, 30, 30)),
                CornerRadius = new CornerRadius(8),
                Child = _countText
            };

            PositionBottomRight();
        }

        public void ShowCountdown(int seconds)
        {
            UpdateCount(seconds);
            PositionBottomRight();

            if (!IsVisible)
            {
                ShowActivated = false;
                Show();
            }

            EnsureTopmostNoActivate();
        }

        public void UpdateCount(int count)
        {
            _countText.Text = count.ToString();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            EnableClickThroughNoActivate();
        }

        private void PositionBottomRight()
        {
            var screen = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position).WorkingArea;
            Left = screen.Right - Width - 24;
            Top = screen.Bottom - Height - 24;
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
                (int)Width,
                (int)Height,
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
