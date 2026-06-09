using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using F2B.Browser.IExplore.COM;

namespace IE.Inspector.Services
{
    /// <summary>
    /// 在 MSHTML 文档内用 JavaScript 做稳定 hit-test（不修改 DOM），坐标换算供外部 overlay 使用。
    /// </summary>
    internal static class IeJsElementHitTest
    {
        private const char FieldSep = '\t';

        private static readonly object Sync = new object();
        private static readonly Dictionary<long, ViewportOffset> OffsetCache = new Dictionary<long, ViewportOffset>();

        public sealed class JsHitResult
        {
            public bool Success { get; set; }
            public string Id { get; set; }
            public string Name { get; set; }
            public string Tag { get; set; }
            public IList<object> FramePath { get; set; }
            public Rectangle Bounds { get; set; }
        }

        private sealed class ViewportOffset
        {
            public double X { get; set; }
            public double Y { get; set; }
        }

        public static JsHitResult HitTestAtPoint(IEWindowController controller, int screenX, int screenY)
        {
            if (controller == null || !controller.doc_hwnd.HasValue)
                return null;

            var fallbackDocHwnd = new IntPtr(controller.doc_hwnd.Value);
            var docHwnd = ResolveActiveDocHwnd(fallbackDocHwnd, screenX, screenY);
            var origin = ClientToScreen(docHwnd, 0, 0);
            var hostClient = ScreenToClient(docHwnd, screenX, screenY);
            var offset = EnsureViewportOffset(controller, docHwnd);

            var script = BuildHitTestScript();
            var args = new List<object>
            {
                hostClient.X,
                hostClient.Y,
                offset.X,
                offset.Y,
                origin.X,
                origin.Y
            };

            try
            {
                var raw = controller.run_js(script, frame_path: null, args: args)?.ToString();
                return ParseResult(raw, origin.X, origin.Y, offset.X, offset.Y);
            }
            catch
            {
                return null;
            }
        }

        private static ViewportOffset EnsureViewportOffset(IEWindowController controller, IntPtr docHwnd)
        {
            var key = docHwnd.ToInt64();
            lock (Sync)
            {
                if (OffsetCache.TryGetValue(key, out var cached))
                    return cached;
            }

            var measured = MeasureViewportOffset(controller);
            lock (Sync)
                OffsetCache[key] = measured;

            return measured;
        }

        private static ViewportOffset MeasureViewportOffset(IEWindowController controller)
        {
            try
            {
                var raw = controller.run_js(MeasureOffsetScript, frame_path: null)?.ToString();
                if (string.IsNullOrWhiteSpace(raw))
                    return new ViewportOffset();

                var parts = raw.Split(FieldSep);
                if (parts.Length < 2)
                    return new ViewportOffset();

                return new ViewportOffset
                {
                    X = ParseDouble(parts[0]),
                    Y = ParseDouble(parts[1])
                };
            }
            catch
            {
                return new ViewportOffset();
            }
        }

        private static JsHitResult ParseResult(string raw, int originScreenX, int originScreenY, double offsetX, double offsetY)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            var parts = raw.Split(FieldSep);
            if (parts.Length < 1 || parts[0] != "1")
                return null;

            if (parts.Length < 8)
                return new JsHitResult { Success = true };

            var left = ParseDouble(parts[4]);
            var top = ParseDouble(parts[5]);
            var width = ParseDouble(parts[6]);
            var height = ParseDouble(parts[7]);

            var screenLeft = (int)Math.Round(originScreenX + offsetX + left);
            var screenTop = (int)Math.Round(originScreenY + offsetY + top);

            return new JsHitResult
            {
                Success = true,
                Id = parts[1],
                Name = parts[2],
                Tag = parts[3],
                FramePath = ParseFramePath(parts.Length >= 9 ? parts[8] : null),
                Bounds = Rectangle.FromLTRB(
                    screenLeft,
                    screenTop,
                    screenLeft + (int)Math.Max(1, Math.Round(width)),
                    screenTop + (int)Math.Max(1, Math.Round(height)))
            };
        }

        private static IList<object> ParseFramePath(string raw)
        {
            var path = new List<object>();
            if (string.IsNullOrWhiteSpace(raw))
                return path;

            foreach (var segment in raw.Split('\x1f'))
            {
                if (string.IsNullOrWhiteSpace(segment))
                    continue;

                if (int.TryParse(segment, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
                    path.Add(index);
                else
                    path.Add(segment);
            }

            return path;
        }

        private static IEWindowController.IEDomElement ResolveElement(IEWindowController controller, JsHitResult hit, IList<object> framePath)
        {
            if (controller == null || hit == null || !hit.Success)
                return null;

            var locator = BuildLocator(hit);
            if (locator.Count == 0)
                return null;

            var matches = controller.find_elements(locator, framePath);
            if (matches == null || matches.Length == 0)
                return null;

            if (matches.Length == 1 || string.IsNullOrEmpty(hit.Id))
                return matches[0];

            foreach (var match in matches)
            {
                try
                {
                    if (string.Equals(match.get_attribute("id")?.ToString(), hit.Id, StringComparison.OrdinalIgnoreCase))
                        return match;
                }
                catch
                {
                }
            }

            return matches[0];
        }

        private static Dictionary<string, object> BuildLocator(JsHitResult hit)
        {
            var locator = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(hit.Id))
            {
                locator["id"] = hit.Id;
                return locator;
            }

            if (!string.IsNullOrWhiteSpace(hit.Name))
                locator["name"] = hit.Name;

            if (!string.IsNullOrWhiteSpace(hit.Tag))
                locator["tag"] = hit.Tag.ToLowerInvariant();

            return locator;
        }

        public static IeDetectResult BuildDetectResult(IEWindowController controller, int screenX, int screenY)
        {
            var jsHit = HitTestAtPoint(controller, screenX, screenY);
            if (jsHit == null || !jsHit.Success)
                return null;

            var framePath = jsHit.FramePath ?? new List<object>();
            var element = ResolveElement(controller, jsHit, framePath);
            if (element == null)
                return null;

            var resolvedFramePath = element.build_frame_path();
            if (resolvedFramePath != null && resolvedFramePath.Count > 0)
                framePath = new List<object>(resolvedFramePath);

            var bounds = ResolveScreenBounds(controller, element, framePath, jsHit.Bounds, screenX, screenY);

            return new IeDetectResult
            {
                Controller = controller,
                Element = element,
                FramePath = framePath,
                Bounds = bounds,
                Status = IeDetectStatus.Hit
            };
        }

        private static Rectangle ResolveScreenBounds(
            IEWindowController controller,
            IEWindowController.IEDomElement element,
            IList<object> framePath,
            Rectangle jsBounds,
            int screenX,
            int screenY)
        {
            var fromCom = controller.get_element_screen_bounds(element, framePath);
            if (fromCom.HasValue && IsReasonableBounds(fromCom.Value, screenX, screenY))
                return fromCom.Value;

            if (!jsBounds.IsEmpty && IsReasonableBounds(jsBounds, screenX, screenY))
                return jsBounds;

            if (fromCom.HasValue && !fromCom.Value.IsEmpty)
                return fromCom.Value;

            return jsBounds;
        }

        private static bool IsReasonableBounds(Rectangle bounds, int screenX, int screenY)
        {
            if (bounds.IsEmpty || bounds.Width < 2 || bounds.Height < 2)
                return false;

            const int tolerance = 48;
            return screenX >= bounds.Left - tolerance
                && screenX < bounds.Right + tolerance
                && screenY >= bounds.Top - tolerance
                && screenY < bounds.Bottom + tolerance;
        }

        public static void ResetSession()
        {
            lock (Sync)
                OffsetCache.Clear();
        }

        private static double ParseDouble(string value)
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
            if (double.TryParse(value, out parsed))
                return parsed;
            return 0;
        }

        private static string BuildHitTestScript()
        {
            return @"
function(hostX, hostY, offsetX, offsetY, originX, originY) {
  var frameSep = '\x1f';

  function pickBest(doc, x, y) {
    var list = null;
    try {
      if (doc.msElementsFromPoint) list = doc.msElementsFromPoint(x, y);
    } catch (e) {}
    if (!list || !list.length) {
      var one = doc.elementFromPoint(x, y);
      list = one ? [one] : [];
    }

    var best = null;
    var bestArea = Infinity;
    for (var i = 0; i < list.length; i++) {
      var el = list[i];
      while (el && el.nodeType !== 1) el = el.parentElement;
      if (!el) continue;
      var tag = (el.tagName || '').toUpperCase();
      if (tag === 'HTML') continue;
      var r = el.getBoundingClientRect();
      if (x >= r.left && x <= r.right && y >= r.top && y <= r.bottom) {
        var area = Math.max(1, r.width * r.height);
        if (area < bestArea) {
          bestArea = area;
          best = el;
        }
      }
    }
    return best;
  }

  function frameRef(el) {
    if (el.name) return el.name;
    if (el.id) return el.id;
    return '0';
  }

  function hit(doc, x, y, pathParts, accumX, accumY) {
    var best = pickBest(doc, x, y);
    if (!best) return null;

    var tag = (best.tagName || '').toUpperCase();
    if (tag === 'IFRAME' || tag === 'FRAME') {
      var fr = best.getBoundingClientRect();
      var inner = null;
      try { inner = best.contentDocument; } catch (e1) {}
      if (!inner) {
        try { inner = best.document; } catch (e2) {}
      }
      if (!inner) return null;

      pathParts.push(frameRef(best));
      return hit(inner, x - fr.left, y - fr.top, pathParts, accumX + fr.left, accumY + fr.top);
    }

    var br = best.getBoundingClientRect();
    return {
      id: best.id || '',
      name: best.name || '',
      tag: (best.tagName || '').toLowerCase(),
      left: accumX + br.left,
      top: accumY + br.top,
      width: br.width,
      height: br.height,
      path: pathParts.join(frameSep)
    };
  }

  var vx = hostX - offsetX;
  var vy = hostY - offsetY;
  var result = hit(document, vx, vy, [], 0, 0);
  if (!result) return '0';

  return ['1', result.id, result.name, result.tag,
    String(result.left), String(result.top), String(result.width), String(result.height),
    result.path].join('\t');
}";
        }

        private const string MeasureOffsetScript = @"
(function() {
  if (!document.body) return '0\t0';
  try {
    var range = document.body.createTextRange();
    range.collapse(true);
    return String(range.boundingLeft) + '\t' + String(range.boundingTop);
  } catch (e) {}
  try {
    var r = document.body.getBoundingClientRect();
    return String(r.left) + '\t' + String(r.top);
  } catch (e2) {}
  return '0\t0';
})();";

        private static IntPtr ResolveActiveDocHwnd(IntPtr fallbackDocHwnd, int screenX, int screenY)
        {
            var point = new NativeMethods.POINT { X = screenX, Y = screenY };
            var hwnd = NativeMethods.WindowFromPoint(point);
            while (hwnd != IntPtr.Zero)
            {
                if (IsIeServerHwnd(hwnd))
                    return hwnd;

                hwnd = NativeMethods.GetParent(hwnd);
            }

            return fallbackDocHwnd;
        }

        private static bool IsIeServerHwnd(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
                return false;

            var builder = new StringBuilder(256);
            NativeMethods.GetClassName(hwnd, builder, builder.Capacity);
            return string.Equals(builder.ToString(), "Internet Explorer_Server", StringComparison.OrdinalIgnoreCase);
        }

        private static NativeMethods.POINT ClientToScreen(IntPtr hwnd, int clientX, int clientY)
        {
            var point = new NativeMethods.POINT { X = clientX, Y = clientY };
            NativeMethods.ClientToScreen(hwnd, ref point);
            return point;
        }

        private static NativeMethods.POINT ScreenToClient(IntPtr hwnd, int screenX, int screenY)
        {
            var point = new NativeMethods.POINT { X = screenX, Y = screenY };
            NativeMethods.ScreenToClient(hwnd, ref point);
            return point;
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
            public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

            [DllImport("user32.dll")]
            public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

            [DllImport("user32.dll")]
            public static extern IntPtr WindowFromPoint(POINT point);

            [DllImport("user32.dll")]
            public static extern IntPtr GetParent(IntPtr hWnd);

            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
        }
    }
}
