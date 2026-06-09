using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using IE.Inspector.Helpers;
using MediaColor = System.Windows.Media.Color;

namespace IE.Inspector.Overlays
{
    /// <summary>
    /// Indicate 模式下在目标元素位置显示半透明高亮遮罩（鼠标穿透，不修改页面 DOM，不抢焦点）。
    /// </summary>
    public sealed class IndicateOverlay : Window
    {
        private static readonly SolidColorBrush BorderBrushColor = CreateFrozenBrush(240, 255, 165, 0);
        private static readonly SolidColorBrush FillBrushColor = CreateFrozenBrush(77, 173, 216, 230);

        public IndicateOverlay()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ShowActivated = false;
            IsHitTestVisible = false;
            ResizeMode = ResizeMode.NoResize;
            Left = -32000;
            Top = -32000;
            Width = 1;
            Height = 1;

            Content = new Border
            {
                Background = FillBrushColor,
                BorderBrush = BorderBrushColor,
                BorderThickness = new Thickness(4),
                CornerRadius = new CornerRadius(0)
            };
        }

        public void UpdateHighlight(Rectangle bounds)
        {
            if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
            {
                HideHighlight();
                return;
            }

            Left = bounds.X;
            Top = bounds.Y;
            Width = Math.Max(4, bounds.Width);
            Height = Math.Max(4, bounds.Height);

            if (!IsVisible)
            {
                ShowActivated = false;
                Show();
            }

            UpdateLayout();
            SyncNativeWindowBounds();
        }

        public void HideHighlight()
        {
            if (IsVisible)
                Hide();
        }

        private void SyncNativeWindowBounds()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            var height = ActualHeight > 0 ? ActualHeight : Height;
            NativeMethods.SetWindowPos(
                hwnd,
                NativeMethods.HWND_TOPMOST,
                (int)Left,
                (int)Top,
                (int)Math.Ceiling(Width),
                (int)Math.Ceiling(height),
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
        }

        private void EnsureTopmostNoActivate()
        {
            SyncNativeWindowBounds();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            EnableClickThrough();
        }

        private void EnableClickThrough()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            const int wsExTransparent = 0x00000020;
            const int wsExNoActivate = 0x08000000;
            const int wsExToolWindow = 0x00000080;
            OverlayNativeMethods.SetExtendedStyle(hwnd, wsExTransparent | wsExNoActivate | wsExToolWindow);
        }

        private static SolidColorBrush CreateFrozenBrush(byte a, byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(MediaColor.FromArgb(a, r, g, b));
            brush.Freeze();
            return brush;
        }

        private static class OverlayNativeMethods
        {
            private const int GwlExstyle = -20;

            [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
            private static extern IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

            [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
            private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

            [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
            private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

            [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
            private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

            public static void SetExtendedStyle(IntPtr hwnd, int flags)
            {
                var get = IntPtr.Size == 8
                    ? (Func<IntPtr, int, IntPtr>)GetWindowLongPtr64
                    : GetWindowLong32;
                var set = IntPtr.Size == 8
                    ? (Func<IntPtr, int, IntPtr, IntPtr>)SetWindowLongPtr64
                    : SetWindowLong32;

                var style = get(hwnd, GwlExstyle);
                set(hwnd, GwlExstyle, new IntPtr(style.ToInt64() | (long)(uint)flags));
            }
        }
    }
}
