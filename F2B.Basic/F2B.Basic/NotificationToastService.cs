using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DrawingColor = System.Drawing.Color;
using FormsCursor = System.Windows.Forms.Cursor;
using FormsScreen = System.Windows.Forms.Screen;

namespace F2B.Basic
{
    /// <summary>
    /// STA WPF host and per-screen toast stack for <see cref="NotificationActivity"/>.
    /// Uses <see cref="FormsScreen.WorkingArea"/> so toasts never cover the taskbar (any edge).
    /// WPF AllowsTransparency gives true-color anti-aliased text without chromakey fringes.
    /// </summary>
    internal static class NotificationToastService
    {
        private const int ScreenMarginPx = 12;
        private const int StackGapPx = 0;
        private const double ContentPaddingDip = 2;
        private const int DefaultFadeMs = 400;

        private static readonly object Sync = new object();
        private static readonly Dictionary<string, List<NotificationToastWindow>> Stacks =
            new Dictionary<string, List<NotificationToastWindow>>(StringComparer.OrdinalIgnoreCase);

        private static Thread _uiThread;
        private static Dispatcher _dispatcher;
        private static ManualResetEventSlim _ready;

        public static void Show(
            string message,
            DrawingColor textColor,
            float fontSize,
            bool bold,
            int timeoutMs,
            int maxWidth,
            int maxHeight)
        {
            EnsureUiThread();

            string text = message ?? string.Empty;
            DrawingColor color = textColor.IsEmpty ? DrawingColor.Red : textColor;
            float size = fontSize <= 0 ? 30f : fontSize;
            int timeout = timeoutMs < 0 ? 0 : timeoutMs;
            int widthCap = maxWidth <= 0 ? 360 : maxWidth;
            int heightCap = maxHeight <= 0 ? 200 : maxHeight;

            _dispatcher.BeginInvoke(
                new Action(() => ShowCore(text, color, size, bold, timeout, widthCap, heightCap)),
                DispatcherPriority.Normal);
        }

        private static void EnsureUiThread()
        {
            ManualResetEventSlim ready;
            lock (Sync)
            {
                if (_uiThread != null && _uiThread.IsAlive && _dispatcher != null)
                {
                    ready = _ready;
                }
                else
                {
                    _ready = new ManualResetEventSlim(false);
                    ready = _ready;
                    _dispatcher = null;
                    _uiThread = new Thread(UiThreadMain)
                    {
                        IsBackground = true,
                        Name = "F2B.Basic.NotificationUI"
                    };
                    _uiThread.SetApartmentState(ApartmentState.STA);
                    _uiThread.Start();
                }
            }

            if (ready == null || !ready.Wait(10000) || _dispatcher == null)
            {
                throw new TimeoutException("Notification UI thread did not start within 10 seconds.");
            }
        }

        private static void UiThreadMain()
        {
            try
            {
                _dispatcher = Dispatcher.CurrentDispatcher;
                if (_ready != null)
                {
                    _ready.Set();
                }

                Dispatcher.Run();
            }
            catch
            {
                if (_ready != null)
                {
                    _ready.Set();
                }
            }
        }

        private static void ShowCore(
            string message,
            DrawingColor textColor,
            float fontSize,
            bool bold,
            int timeoutMs,
            int maxWidthPx,
            int maxHeightPx)
        {
            var cursor = FormsCursor.Position;
            FormsScreen screen = FormsScreen.FromPoint(cursor) ?? FormsScreen.PrimaryScreen;
            var work = screen.WorkingArea;
            string screenKey = screen.DeviceName ?? work.ToString();

            double dpiScale = GetDpiScale(screen);
            double maxWidthDip = Math.Max(8, maxWidthPx / dpiScale);
            double maxHeightDip = Math.Max(8, maxHeightPx / dpiScale);

            string displayText = FitText(message, fontSize, bold, maxWidthDip, maxHeightDip, dpiScale);

            var toast = new NotificationToastWindow(
                displayText,
                ToMediaColor(textColor),
                fontSize,
                bold,
                maxWidthDip,
                maxHeightDip,
                ContentPaddingDip,
                timeoutMs,
                DefaultFadeMs,
                screenKey,
                dpiScale);

            toast.Closed += OnToastClosed;

            // Shrink-wrap to text before stacking (SizeToContent).
            toast.Measure(new Size(maxWidthDip, maxHeightDip));
            toast.Arrange(new Rect(0, 0, toast.DesiredSize.Width, toast.DesiredSize.Height));
            toast.UpdateLayout();
            toast.CaptureActualSize();

            lock (Sync)
            {
                List<NotificationToastWindow> stack;
                if (!Stacks.TryGetValue(screenKey, out stack))
                {
                    stack = new List<NotificationToastWindow>();
                    Stacks[screenKey] = stack;
                }

                stack.Insert(0, toast);
                LayoutStack(screenKey, work, dpiScale);
            }

            toast.ShowInactive();
            toast.CaptureActualSize();
            lock (Sync)
            {
                LayoutStack(screenKey, work, toast.DpiScale);
            }

            toast.BeginLife();
        }

        private static void OnToastClosed(object sender, EventArgs e)
        {
            var toast = sender as NotificationToastWindow;
            if (toast == null)
            {
                return;
            }

            toast.Closed -= OnToastClosed;
            string screenKey = toast.ScreenKey;
            FormsScreen screen = FormsScreen.AllScreens.FirstOrDefault(s =>
                string.Equals(s.DeviceName, screenKey, StringComparison.OrdinalIgnoreCase))
                ?? FormsScreen.FromPoint(FormsCursor.Position)
                ?? FormsScreen.PrimaryScreen;

            lock (Sync)
            {
                List<NotificationToastWindow> stack;
                if (Stacks.TryGetValue(screenKey, out stack))
                {
                    stack.Remove(toast);
                    if (stack.Count == 0)
                    {
                        Stacks.Remove(screenKey);
                    }
                    else if (screen != null)
                    {
                        LayoutStack(screenKey, screen.WorkingArea, toast.DpiScale);
                    }
                }
            }
        }

        private static void LayoutStack(string screenKey, System.Drawing.Rectangle workPx, double dpiScale)
        {
            List<NotificationToastWindow> stack;
            if (!Stacks.TryGetValue(screenKey, out stack) || stack.Count == 0)
            {
                return;
            }

            int bottomPx = workPx.Bottom - ScreenMarginPx;
            for (int i = 0; i < stack.Count; i++)
            {
                NotificationToastWindow toast = stack[i];
                toast.CaptureActualSize();

                int widthPx = Math.Max(1, (int)Math.Ceiling(toast.WidthDip * dpiScale));
                int heightPx = Math.Max(1, (int)Math.Ceiling(toast.HeightDip * dpiScale));
                int xPx = workPx.Right - widthPx - ScreenMarginPx;
                int yPx = bottomPx - heightPx;
                if (yPx < workPx.Top + ScreenMarginPx)
                {
                    yPx = workPx.Top + ScreenMarginPx;
                }

                toast.SetPixelPosition(xPx, yPx);
                bottomPx = yPx - StackGapPx;
            }
        }

        private static double GetDpiScale(FormsScreen screen)
        {
            try
            {
                // Approximate: primary desktop DPI. Per-monitor variance is handled after HWND exists.
                using (var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero))
                {
                    if (g.DpiX > 0)
                    {
                        return g.DpiX / 96.0;
                    }
                }
            }
            catch
            {
            }

            return 1.0;
        }

        private static Color ToMediaColor(DrawingColor color)
        {
            return Color.FromArgb(255, color.R, color.G, color.B);
        }

        private static string FitText(
            string message,
            float fontSize,
            bool bold,
            double maxWidthDip,
            double maxHeightDip,
            double dpiScale)
        {
            double contentMaxWidth = Math.Max(8, maxWidthDip - (ContentPaddingDip * 2) - 2);
            double contentMaxHeight = Math.Max(8, maxHeightDip - (ContentPaddingDip * 2) - 2);
            string source = message ?? string.Empty;

            Size full = MeasureText(source, fontSize, bold, contentMaxWidth, dpiScale);
            if (full.Height <= contentMaxHeight + 0.5)
            {
                return source;
            }

            if (source.Length == 0)
            {
                return string.Empty;
            }

            int low = 0;
            int high = source.Length;
            string best = "...";

            while (low <= high)
            {
                int mid = (low + high) / 2;
                string candidate = source.Substring(0, mid).TrimEnd() + "...";
                Size size = MeasureText(candidate, fontSize, bold, contentMaxWidth, dpiScale);
                if (size.Height <= contentMaxHeight + 0.5)
                {
                    best = candidate;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return best;
        }

        private static Size MeasureText(string text, float fontSize, bool bold, double maxWidthDip, double dpiScale)
        {
            double pixelsPerDip = dpiScale <= 0 ? 1.0 : dpiScale;
            var ft = new FormattedText(
                text ?? string.Empty,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(
                    new FontFamily("Segoe UI"),
                    FontStyles.Normal,
                    bold ? FontWeights.Bold : FontWeights.Normal,
                    FontStretches.Normal),
                fontSize,
                Brushes.Black,
                pixelsPerDip);

            ft.MaxTextWidth = Math.Max(1, maxWidthDip);
            ft.Trimming = TextTrimming.None;
            return new Size(ft.Width, ft.Height);
        }

        private sealed class NotificationToastWindow : Window
        {
            private readonly TextBlock _textBlock;
            private readonly DispatcherTimer _lifeTimer;
            private readonly DispatcherTimer _topMostTimer;
            private readonly int _fadeMs;
            private readonly bool _autoDismiss;
            private bool _fading;

            public NotificationToastWindow(
                string text,
                Color textColor,
                float fontSize,
                bool bold,
                double maxWidthDip,
                double maxHeightDip,
                double paddingDip,
                int timeoutMs,
                int fadeMs,
                string screenKey,
                double dpiScale)
            {
                ScreenKey = screenKey;
                DpiScale = dpiScale <= 0 ? 1.0 : dpiScale;
                _fadeMs = fadeMs <= 0 ? DefaultFadeMs : fadeMs;
                _autoDismiss = timeoutMs > 0;

                WindowStyle = WindowStyle.None;
                AllowsTransparency = true;
                Background = Brushes.Transparent;
                Topmost = true;
                ShowInTaskbar = false;
                ResizeMode = ResizeMode.NoResize;
                ShowActivated = false;
                Focusable = false;
                IsHitTestVisible = false;
                SizeToContent = SizeToContent.WidthAndHeight;
                MaxWidth = Math.Max(8, maxWidthDip);
                MaxHeight = Math.Max(8, maxHeightDip);
                Opacity = 1.0;
                SnapsToDevicePixels = true;
                UseLayoutRounding = true;

                _textBlock = new TextBlock
                {
                    Text = text ?? string.Empty,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = fontSize,
                    FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = new SolidColorBrush(textColor),
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Left,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0),
                    Padding = new Thickness(0),
                    IsHitTestVisible = false
                };

                Content = new Border
                {
                    BorderThickness = new Thickness(0),
                    Background = Brushes.Transparent,
                    Padding = new Thickness(paddingDip),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Child = _textBlock
                };

                _lifeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(1, timeoutMs)) };
                if (_autoDismiss)
                {
                    _lifeTimer.Tick += (s, e) =>
                    {
                        _lifeTimer.Stop();
                        BeginFade();
                    };
                }

                // Re-assert HWND_TOPMOST while visible so other windows cannot bury the toast.
                _topMostTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                _topMostTimer.Tick += (s, e) => EnsureTopMost();

                SourceInitialized += OnSourceInitialized;
            }

            public string ScreenKey { get; private set; }

            public double DpiScale { get; private set; }

            public double WidthDip { get; private set; }

            public double HeightDip { get; private set; }

            public void CaptureActualSize()
            {
                UpdateLayout();
                double w = ActualWidth > 0 ? ActualWidth : DesiredSize.Width;
                double h = ActualHeight > 0 ? ActualHeight : DesiredSize.Height;
                if (w > 0)
                {
                    WidthDip = w;
                }

                if (h > 0)
                {
                    HeightDip = h;
                }
            }

            public void ShowInactive()
            {
                Show();
                EnsureTopMost();
                _topMostTimer.Start();
            }

            public void BeginLife()
            {
                if (_autoDismiss)
                {
                    _lifeTimer.Start();
                }
            }

            public void SetPixelPosition(int xPx, int yPx)
            {
                var helper = new WindowInteropHelper(this);
                if (helper.Handle == IntPtr.Zero)
                {
                    Left = xPx / DpiScale;
                    Top = yPx / DpiScale;
                    return;
                }

                RefreshDpiScale(helper.Handle);
                Left = xPx / DpiScale;
                Top = yPx / DpiScale;

                const uint SwpNoActivate = 0x0010;
                const uint SwpNoSize = 0x0001;
                const uint SwpShowWindow = 0x0040;
                NativeMethods.SetWindowPos(
                    helper.Handle,
                    NativeMethods.HwndTopMost,
                    xPx,
                    yPx,
                    0,
                    0,
                    SwpNoActivate | SwpNoSize | SwpShowWindow);
            }

            private void OnSourceInitialized(object sender, EventArgs e)
            {
                var helper = new WindowInteropHelper(this);
                RefreshDpiScale(helper.Handle);

                int ex = NativeMethods.GetWindowLong(helper.Handle, NativeMethods.GwlExStyle);
                // WS_EX_TRANSPARENT: mouse hit-test passes through to windows beneath.
                ex |= NativeMethods.WsExNoActivate
                    | NativeMethods.WsExToolWindow
                    | NativeMethods.WsExTopMost
                    | NativeMethods.WsExTransparent
                    | NativeMethods.WsExLayered;
                NativeMethods.SetWindowLong(helper.Handle, NativeMethods.GwlExStyle, ex);
                EnsureTopMost();
            }

            private void EnsureTopMost()
            {
                if (!IsVisible)
                {
                    return;
                }

                // Toggle refreshes WPF's topmost bit when another topmost window steals z-order.
                Topmost = false;
                Topmost = true;

                var helper = new WindowInteropHelper(this);
                if (helper.Handle == IntPtr.Zero)
                {
                    return;
                }

                const uint SwpNoActivate = 0x0010;
                const uint SwpNoMove = 0x0002;
                const uint SwpNoSize = 0x0001;
                const uint SwpShowWindow = 0x0040;
                NativeMethods.SetWindowPos(
                    helper.Handle,
                    NativeMethods.HwndTopMost,
                    0,
                    0,
                    0,
                    0,
                    SwpNoActivate | SwpNoMove | SwpNoSize | SwpShowWindow);
            }

            private void RefreshDpiScale(IntPtr hwnd)
            {
                try
                {
                    uint dpi = NativeMethods.GetDpiForWindow(hwnd);
                    if (dpi > 0)
                    {
                        DpiScale = dpi / 96.0;
                    }
                }
                catch
                {
                    // GetDpiForWindow requires Win10+; keep existing scale.
                }
            }

            private void BeginFade()
            {
                if (_fading)
                {
                    return;
                }

                _fading = true;
                var anim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(_fadeMs))
                {
                    FillBehavior = FillBehavior.Stop
                };
                anim.Completed += (s, e) => Close();
                BeginAnimation(OpacityProperty, anim);
            }

            protected override void OnClosed(EventArgs e)
            {
                _lifeTimer.Stop();
                _topMostTimer.Stop();
                base.OnClosed(e);
            }
        }

        private static class NativeMethods
        {
            public static readonly IntPtr HwndTopMost = new IntPtr(-1);

            public const int GwlExStyle = -20;
            public const int WsExNoActivate = 0x08000000;
            public const int WsExToolWindow = 0x00000080;
            public const int WsExTopMost = 0x00000008;
            public const int WsExTransparent = 0x00000020;
            public const int WsExLayered = 0x00080000;

            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool SetWindowPos(
                IntPtr hWnd,
                IntPtr hWndInsertAfter,
                int x,
                int y,
                int cx,
                int cy,
                uint uFlags);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

            [DllImport("user32.dll")]
            public static extern uint GetDpiForWindow(IntPtr hWnd);
        }
    }
}
