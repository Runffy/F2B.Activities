using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;

namespace FlaUI.Inspector.Overlays
{
    /// <summary>
    /// Indicate 模式下在目标元素位置显示半透明高亮框（鼠标穿透，不抢焦点）。
    /// 使用 WPF 透明窗口以正确呈现 alpha 通道。
    /// </summary>
    public sealed class IndicateOverlay : Window
    {
        // 橙黄色边框，高不透明度以便 Indicate 时清晰可见
        private static readonly SolidColorBrush BorderBrushColor = CreateFrozenBrush(240, 255, 165, 0);
        // 淡蓝色填充，70% 透明（30% 不透明度）
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
            Width = Math.Max(1, bounds.Width);
            Height = Math.Max(1, bounds.Height);

            if (!IsVisible)
            {
                ShowActivated = false;
                Show();
            }

            EnsureTopmostNoActivate();
        }

        public void HideHighlight()
        {
            if (IsVisible)
                Hide();
        }

        private void EnsureTopmostNoActivate()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            FlaUI.Inspector.Helpers.NativeMethods.SetWindowPos(
                hwnd,
                FlaUI.Inspector.Helpers.NativeMethods.HWND_TOPMOST,
                0,
                0,
                0,
                0,
                FlaUI.Inspector.Helpers.NativeMethods.SWP_NOMOVE |
                FlaUI.Inspector.Helpers.NativeMethods.SWP_NOSIZE |
                FlaUI.Inspector.Helpers.NativeMethods.SWP_NOACTIVATE |
                FlaUI.Inspector.Helpers.NativeMethods.SWP_SHOWWINDOW);
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

            NativeMethods.SetExtendedStyle(hwnd, wsExTransparent | wsExNoActivate | wsExToolWindow);
        }

        private static SolidColorBrush CreateFrozenBrush(byte a, byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(MediaColor.FromArgb(a, r, g, b));
            brush.Freeze();
            return brush;
        }

        private static class NativeMethods
        {
            private const int GwlExstyle = -20;

            [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLong")]
            private static extern IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

            [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
            private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

            [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLong")]
            private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

            [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
            private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

            public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
            {
                return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLong32(hWnd, nIndex);
            }

            public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
            {
                return IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : SetWindowLong32(hWnd, nIndex, dwNewLong);
            }

            public static void SetExtendedStyle(IntPtr hwnd, int flags)
            {
                var style = GetWindowLongPtr(hwnd, GwlExstyle);
                SetWindowLongPtr(hwnd, GwlExstyle, new IntPtr(style.ToInt64() | (long)(uint)flags));
            }
        }
    }
}
