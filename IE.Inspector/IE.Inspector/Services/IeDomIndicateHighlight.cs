using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading;
using F2B.Browser.IExplore.COM;

namespace IE.Inspector.Services
{
    /// <summary>
    /// Indicate 悬停时在 MSHTML 文档内直接绘制高亮（与 F12 / IE DevTools 同类思路）。
    /// 必须在取得元素的同一 COM/STA 上下文中调用。
    /// </summary>
    internal static class IeDomIndicateHighlight
    {
        private const string MarkerAttribute = "data-ie-inspector-hl";
        private const string HighlightOutline = "2px solid #FFA500";
        private const string HighlightOutlineOffset = "1px";

        private static readonly object Sync = new object();
        private static HighlightState _current;

        public static bool Apply(
            IEWindowController controller,
            IEWindowController.IEDomElement element,
            IList<object> framePath)
        {
            if (controller == null || element == null)
            {
                Clear();
                return false;
            }

            lock (Sync)
            {
                if (_current != null
                    && ReferenceEquals(_current.ElementRaw, element.raw)
                    && ReferenceEquals(_current.Controller, controller))
                {
                    return true;
                }

                ClearLocked();

                var marker = Interlocked.Increment(ref _markerSequence).ToString(CultureInfo.InvariantCulture);
                if (!TrySetMarker(element, marker))
                    return false;

                var path = framePath ?? new List<object>();
                var script = string.Format(
                    CultureInfo.InvariantCulture,
                    @"(function() {{
  var el = document.querySelector('[{0}=""{1}""]');
  if (!el) return false;
  el.__ieInspectorSavedOutline = el.style.outline || '';
  el.__ieInspectorSavedOutlineOffset = el.style.outlineOffset || '';
  el.style.outline = '{2}';
  el.style.outlineOffset = '{3}';
  return true;
}})();",
                    MarkerAttribute,
                    marker,
                    HighlightOutline,
                    HighlightOutlineOffset);

                try
                {
                    var result = controller.run_js(script, path);
                    if (!IsTruthy(result))
                    {
                        TryRemoveMarker(element, marker);
                        return false;
                    }
                }
                catch
                {
                    TryRemoveMarker(element, marker);
                    return false;
                }

                _current = new HighlightState
                {
                    Controller = controller,
                    Element = element,
                    ElementRaw = element.raw,
                    FramePath = path,
                    Marker = marker
                };
                return true;
            }
        }

        public static void Clear()
        {
            lock (Sync)
                ClearLocked();
        }

        private static int _markerSequence;

        private static void ClearLocked()
        {
            if (_current == null)
                return;

            try
            {
                var script = string.Format(
                    CultureInfo.InvariantCulture,
                    @"(function() {{
  var el = document.querySelector('[{0}=""{1}""]');
  if (!el) return;
  if (el.__ieInspectorSavedOutline !== undefined) {{
    el.style.outline = el.__ieInspectorSavedOutline;
    el.style.outlineOffset = el.__ieInspectorSavedOutlineOffset || '';
    delete el.__ieInspectorSavedOutline;
    delete el.__ieInspectorSavedOutlineOffset;
  }}
  el.removeAttribute('{0}');
}})();",
                    MarkerAttribute,
                    _current.Marker);

                _current.Controller.run_js(script, _current.FramePath);
            }
            catch
            {
                TryRemoveMarker(_current.Element, _current.Marker);
            }
            finally
            {
                _current = null;
            }
        }

        private static bool TrySetMarker(IEWindowController.IEDomElement element, string marker)
        {
            try
            {
                InvokeMethod(element.raw, "setAttribute", MarkerAttribute, marker);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void TryRemoveMarker(IEWindowController.IEDomElement element, string marker)
        {
            if (element == null)
                return;

            try
            {
                InvokeMethod(element.raw, "removeAttribute", MarkerAttribute);
            }
            catch
            {
            }
        }

        private static bool IsTruthy(object value)
        {
            if (value == null)
                return false;
            if (value is bool b)
                return b;

            return !string.Equals(value.ToString(), "false", StringComparison.OrdinalIgnoreCase);
        }

        private static object InvokeMethod(object instance, string methodName, params object[] args)
        {
            return instance.GetType().InvokeMember(
                methodName,
                BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance,
                null,
                instance,
                args);
        }

        private sealed class HighlightState
        {
            public IEWindowController Controller { get; set; }
            public IEWindowController.IEDomElement Element { get; set; }
            public object ElementRaw { get; set; }
            public IList<object> FramePath { get; set; }
            public string Marker { get; set; }
        }
    }
}
