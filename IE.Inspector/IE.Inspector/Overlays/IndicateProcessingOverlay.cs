using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using IE.Inspector.Helpers;
using MediaColor = System.Windows.Media.Color;

namespace IE.Inspector.Overlays
{
    /// <summary>
    /// Indicate 点击确认后，在分析 selector 结构期间于屏幕正中显示进度提示。
    /// </summary>
    public sealed class IndicateProcessingOverlay : Window
    {
        private const string BaseMessage = "正在分析元素结构，请稍候";
        private const double MaxPanelWidth = 420;

        private readonly Border _panel;
        private readonly TextBlock _messageText;
        private readonly DispatcherTimer _dotsTimer;
        private int _dotCount;

        public IndicateProcessingOverlay()
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
                Text = BaseMessage + "…",
                TextWrapping = TextWrapping.NoWrap,
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Segoe UI"),
                TextAlignment = TextAlignment.Center
            };

            _panel = new Border
            {
                Background = new SolidColorBrush(MediaColor.FromArgb(235, 33, 33, 33)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(16, 10, 16, 10),
                MaxWidth = MaxPanelWidth,
                Child = _messageText
            };

            Content = _panel;

            _dotsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _dotsTimer.Tick += (_, __) =>
            {
                _dotCount = (_dotCount + 1) % 4;
                _messageText.Text = BaseMessage + new string('.', _dotCount == 0 ? 3 : _dotCount);
            };
        }

        public void ShowProcessing()
        {
            _dotCount = 0;
            _messageText.Text = BaseMessage + "…";

            if (!IsVisible)
            {
                ShowActivated = false;
                Show();
            }

            _dotsTimer.Start();
            SyncBoundsToScreenCenter();
        }

        public void HideProcessing()
        {
            _dotsTimer.Stop();
            if (IsVisible)
                Hide();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            EnableClickThroughNoActivate();
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            SyncBoundsToScreenCenter();
        }

        private void SyncBoundsToScreenCenter()
        {
            UpdateLayout();
            _panel.Measure(new Size(MaxPanelWidth, double.PositiveInfinity));
            _panel.Arrange(new Rect(0, 0, _panel.DesiredSize.Width, _panel.DesiredSize.Height));

            var width = Math.Ceiling(_panel.DesiredSize.Width);
            var height = Math.Ceiling(_panel.DesiredSize.Height);
            if (width < 1)
                width = 1;
            if (height < 1)
                height = 1;

            Width = width;
            Height = height;

            var screen = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position).WorkingArea;
            Left = screen.Left + (screen.Width - width) / 2;
            Top = screen.Top + (screen.Height - height) / 2;

            EnsureTopmostNoActivate((int)width, (int)height);
        }

        private void EnsureTopmostNoActivate(int width, int height)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            NativeMethods.SetWindowPos(
                hwnd,
                NativeMethods.HWND_TOPMOST,
                (int)Left,
                (int)Top,
                width,
                height,
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
            return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLongPtr32(hWnd, nIndex);
        }

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            return IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : SetWindowLongPtr32(hWnd, nIndex, dwNewLong);
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    }
}
