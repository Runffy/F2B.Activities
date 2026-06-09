using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using F2B.Browser.IExplore.COM;

namespace IE.Inspector.Services
{
    public enum IeDetectStatus
    {
        None,
        Hit,
        NoMsHtml,
        EdgeIeMode,
        EdgeOrChrome,
        OtherBrowser
    }

    public sealed class IeDetectResult
    {
        public IEWindowController Controller { get; set; }
        public IEWindowController.IEDomElement Element { get; set; }
        public IList<object> FramePath { get; set; }
        public Rectangle Bounds { get; set; }
        public IeDetectStatus Status { get; set; }
    }

    public static class IeElementDetector
    {
        private static readonly object SessionSync = new object();
        private static IEWindowController _sessionController;

        public static void BeginSession()
        {
            lock (SessionSync)
                _sessionController = null;
            IeJsElementHitTest.ResetSession();
        }

        public static void EndSession()
        {
            lock (SessionSync)
                _sessionController = null;
            IeJsElementHitTest.ResetSession();
        }

        public static IeDetectResult DetectAtPointForHover(int screenX, int screenY)
        {
            return DetectAtPoint(screenX, screenY, forClick: false);
        }

        public static IeDetectResult DetectAtPoint(int screenX, int screenY, bool forClick = false)
        {
            try
            {
                if (!forClick && !IEWindowController.has_mshtml_at_screen_point(screenX, screenY))
                    return CreateStatusResult(ProbeStatusAtPoint(screenX, screenY));

                IEWindowController controller;
                lock (SessionSync)
                {
                    if (_sessionController != null && TryDetectWithController(_sessionController, screenX, screenY, out var cached))
                        return cached;

                    controller = forClick
                        ? IEWindowController.connect_embedded_ie_window_at_screen_point(screenX, screenY, timeout: 5000)
                        : IEWindowController.try_connect_embedded_ie_window_at_screen_point(screenX, screenY);

                    if (controller != null)
                        _sessionController = controller;
                }

                if (controller == null)
                    return CreateStatusResult(ProbeStatusAtPoint(screenX, screenY));

                return BuildResult(controller, screenX, screenY) ?? CreateStatusResult(ProbeStatusAtPoint(screenX, screenY));
            }
            catch
            {
                return CreateStatusResult(ProbeStatusAtPoint(screenX, screenY));
            }
        }

        public static string GetHintMessage(int screenX, int screenY)
        {
            if (IEWindowController.has_mshtml_at_screen_point(screenX, screenY))
            {
                if (IsEdgeShellAtPoint(screenX, screenY))
                    return "已检测到 Edge IE 模式（MSHTML），请悬停约 0.2 秒等待高亮（首次连接可能稍慢）。";

                return "已检测到 MSHTML 文档，请悬停约 0.2 秒等待高亮（首次连接可能稍慢）。";
            }

            var status = ProbeStatusAtPoint(screenX, screenY);
            switch (status)
            {
                case IeDetectStatus.EdgeIeMode:
                case IeDetectStatus.EdgeOrChrome:
                    return "当前在 Edge 窗口上，但未检测到 IE 模式文档。请确认地址栏有 IE 图标且页面已加载完成；若仍无效，请让 IT 检查 IE 模式站点策略。";
                case IeDetectStatus.OtherBrowser:
                    return "此位置不是受支持的 IE 页面。请使用 IE11、WebBrowser 嵌入式窗口，或 Edge IE 模式。";
                default:
                    return "此位置未检测到 MSHTML 文档。";
            }
        }

        private static IeDetectStatus ProbeStatusAtPoint(int screenX, int screenY)
        {
            if (IsEdgeShellAtPoint(screenX, screenY))
                return IeDetectStatus.EdgeOrChrome;

            return IeDetectStatus.OtherBrowser;
        }

        private static bool IsEdgeShellAtPoint(int screenX, int screenY)
        {
            var hwnd = GetHwndAtPoint(screenX, screenY);
            while (hwnd != IntPtr.Zero)
            {
                if (IsEdgeShellClass(GetClassName(hwnd)))
                    return true;

                hwnd = NativeMethods.GetParent(hwnd);
            }

            return false;
        }

        private static bool TryDetectWithController(IEWindowController controller, int screenX, int screenY, out IeDetectResult result)
        {
            result = BuildResult(controller, screenX, screenY);
            return result != null && result.Element != null;
        }

        private static IeDetectResult BuildResult(IEWindowController controller, int screenX, int screenY)
        {
            var jsResult = IeJsElementHitTest.BuildDetectResult(controller, screenX, screenY);
            if (jsResult != null)
                return jsResult;

            var hit = controller.try_hit_test_screen_point(screenX, screenY);
            if (hit?.Element == null)
                return null;

            var framePath = hit.FramePath ?? new List<object>();
            var resolvedFramePath = hit.Element.build_frame_path();
            if (resolvedFramePath != null && resolvedFramePath.Count > 0)
                framePath = new List<object>(resolvedFramePath);

            var bounds = controller.get_element_screen_bounds(hit.Element, framePath);
            if (!bounds.HasValue || bounds.Value.IsEmpty || bounds.Value.Width < 4 || bounds.Value.Height < 4)
                bounds = TryEstimateBoundsFromHit(controller, hit, screenX, screenY);

            return new IeDetectResult
            {
                Controller = controller,
                Element = hit.Element,
                FramePath = framePath,
                Bounds = bounds ?? Rectangle.Empty,
                Status = IeDetectStatus.Hit
            };
        }

        private static Rectangle? TryEstimateBoundsFromHit(
            IEWindowController controller,
            IEWindowController.IeScreenHitResult hit,
            int screenX,
            int screenY)
        {
            var measured = controller.get_element_screen_bounds(hit.Element, hit.FramePath);
            if (measured.HasValue && !measured.Value.IsEmpty && measured.Value.Width >= 4 && measured.Value.Height >= 4)
                return measured;

            try
            {
                var width = ReadDouble(hit.Element.get_attribute("offsetWidth")) ?? 80;
                var height = ReadDouble(hit.Element.get_attribute("offsetHeight")) ?? 24;
                width = Math.Max(8, width);
                height = Math.Max(8, height);
                return new Rectangle(
                    screenX - (int)Math.Round(width / 2),
                    screenY - (int)Math.Round(height / 2),
                    (int)Math.Round(width),
                    (int)Math.Round(height));
            }
            catch
            {
                return new Rectangle(screenX - 40, screenY - 12, 80, 24);
            }
        }

        private static double? ReadDouble(object value)
        {
            if (value == null)
                return null;

            if (value is double d)
                return d;
            if (value is int i)
                return i;
            if (double.TryParse(value.ToString(), out var parsed))
                return parsed;

            return null;
        }

        private static IeDetectResult CreateStatusResult(IeDetectStatus status)
        {
            return new IeDetectResult { Status = status, Bounds = Rectangle.Empty };
        }

        private static IntPtr GetHwndAtPoint(int screenX, int screenY)
        {
            var point = new NativeMethods.POINT { X = screenX, Y = screenY };
            return NativeMethods.WindowFromPoint(point);
        }

        private static string GetClassName(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
                return string.Empty;

            var builder = new StringBuilder(256);
            NativeMethods.GetClassName(hwnd, builder, builder.Capacity);
            return builder.ToString();
        }

        private static bool IsEdgeShellClass(string className)
        {
            if (string.IsNullOrEmpty(className))
                return false;

            return className.IndexOf("Chrome_WidgetWin", StringComparison.OrdinalIgnoreCase) >= 0
                || className.IndexOf("Chrome_RenderWidgetHostHWND", StringComparison.OrdinalIgnoreCase) >= 0
                || string.Equals(className, "ApplicationFrameWindow", StringComparison.OrdinalIgnoreCase);
        }

        public static Rectangle GetBoundingRectangle(IEWindowController controller, IEWindowController.IEDomElement element, IList<object> framePath)
        {
            if (controller == null || element == null)
                return Rectangle.Empty;

            return controller.get_element_screen_bounds(element, framePath) ?? Rectangle.Empty;
        }

        private static class NativeMethods
        {
            [StructLayout(LayoutKind.Sequential)]
            public struct POINT
            {
                public int X;
                public int Y;
            }

            [DllImport("user32.dll")]
            public static extern IntPtr WindowFromPoint(POINT point);

            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

            [DllImport("user32.dll")]
            public static extern IntPtr GetParent(IntPtr hWnd);
        }
    }
}
