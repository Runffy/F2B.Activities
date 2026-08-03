using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using F2B.Browser.Chromium.Cdp.Browser;
using F2B.Browser.Chromium.Cdp.Exceptions;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal static class CdpElementActions
    {
        internal static void Click(
            CdpElement element,
            CdpMouseButton button,
            CdpInteractionMethod method,
            int count)
        {
            var context = element.Context;
            context.PrepareForAction();

            if (method == CdpInteractionMethod.Js)
            {
                context.RunJs(CdpElementScripts.ClickJs);
                return;
            }

            var point = element.Rect.ViewportClickPoint;
            var input = new CdpInputDispatcher(context.Session);
            for (var i = 0; i < Math.Max(1, count); i++)
            {
                input.Click(point.Item1, point.Item2, button, i == 0 ? count : 1);
            }
        }

        internal static void Clear(CdpElement element, CdpInteractionMethod method)
        {
            var context = element.Context;
            context.PrepareForAction();

            if (method == CdpInteractionMethod.Js)
            {
                context.RunJs(CdpElementScripts.ClearValueJs);
                return;
            }

            Focus(element);
            var input = new CdpInputDispatcher(context.Session);
            input.SendKeys(new[] { CdpKey.CtrlA, CdpKey.Del });
        }

        internal static void Input(
            CdpElement element,
            object value,
            bool clear,
            CdpInteractionMethod method)
        {
            var context = element.Context;
            context.PrepareForAction();

            if (IsFileInput(element))
            {
                SetFileInput(element, value);
                return;
            }

            if (method == CdpInteractionMethod.Js)
            {
                if (clear)
                {
                    context.RunJs(CdpElementScripts.ClearValueJs);
                }

                var serializer = new CdpJsonSerializer();
                var literal = serializer.Serialize(ConvertInputValue(value));
                context.RunJs(CdpElementScripts.BuildSetValueJs(literal));
                return;
            }

            Focus(element);
            if (clear)
            {
                Clear(element, CdpInteractionMethod.Simulate);
            }

            var input = new CdpInputDispatcher(context.Session);
            input.InsertText(ConvertInputValue(value));
        }

        internal static void SendKeys(CdpElement element, params CdpKey[] keys)
        {
            var context = element.Context;
            context.PrepareForAction();
            Focus(element);
            new CdpInputDispatcher(context.Session).SendKeys(keys);
        }

        internal static void Focus(CdpElement element)
        {
            var context = element.Context;
            try
            {
                context.Send("DOM.focus", new Dictionary<string, object>
                {
                    { "backendNodeId", element.BackendNodeId }
                });
            }
            catch (BrowserException)
            {
                context.RunJs(CdpElementScripts.FocusJs);
            }
        }

        internal static void Hover(CdpElement element, int? offsetX, int? offsetY)
        {
            var context = element.Context;
            context.PrepareForAction();
            var location = element.Rect.ViewportLocation;
            var size = element.Rect.Size;
            var x = offsetX.HasValue ? location.Item1 + offsetX.Value : element.Rect.ViewportMidpoint.Item1;
            var y = offsetY.HasValue ? location.Item2 + offsetY.Value : element.Rect.ViewportMidpoint.Item2;
            new CdpInputDispatcher(context.Session).Hover(x, y);
        }

        internal static void Drag(CdpElement element, int offsetX, int offsetY, double durationSeconds)
        {
            var context = element.Context;
            context.PrepareForAction();
            var start = element.Rect.ViewportMidpoint;
            new CdpInputDispatcher(context.Session).Drag(
                start.Item1,
                start.Item2,
                start.Item1 + offsetX,
                start.Item2 + offsetY,
                durationSeconds <= 0 ? 0 : durationSeconds);
        }

        internal static void DragToElement(CdpElement element, CdpElement target, double durationSeconds)
        {
            var targetPoint = target.Rect.ViewportMidpoint;
            var start = element.Rect.ViewportMidpoint;
            element.Context.PrepareForAction();
            new CdpInputDispatcher(element.Context.Session).Drag(
                start.Item1,
                start.Item2,
                targetPoint.Item1,
                targetPoint.Item2,
                durationSeconds <= 0 ? 0 : durationSeconds);
        }

        internal static void DragToLocation(CdpElement element, Tuple<int, int> targetLocation, double durationSeconds)
        {
            var start = element.Rect.ViewportMidpoint;
            element.Context.PrepareForAction();
            new CdpInputDispatcher(element.Context.Session).Drag(
                start.Item1,
                start.Item2,
                targetLocation.Item1,
                targetLocation.Item2,
                durationSeconds <= 0 ? 0 : durationSeconds);
        }

        internal static void Check(CdpElement element, bool uncheck, CdpInteractionMethod method)
        {
            var context = element.Context;
            context.PrepareForAction();
            var isChecked = element.States.IsChecked;
            var shouldToggle = (isChecked && uncheck) || (!isChecked && !uncheck);
            if (!shouldToggle)
            {
                return;
            }

            if (method == CdpInteractionMethod.Js)
            {
                context.RunJs(CdpElementScripts.BuildCheckJs(uncheck));
                return;
            }

            Click(element, CdpMouseButton.Left, CdpInteractionMethod.Simulate, 1);
        }

        internal static void Scroll(CdpElement element, CdpScrollDirection direction, int pixel)
        {
            var context = element.Context;
            context.PrepareForAction();
            var amount = Math.Max(0, pixel);
            string script;
            switch (direction)
            {
                case CdpScrollDirection.Up:
                    script = "this.scrollBy(0, -" + amount + ");";
                    break;
                case CdpScrollDirection.Left:
                    script = "this.scrollBy(-" + amount + ", 0);";
                    break;
                case CdpScrollDirection.Right:
                    script = "this.scrollBy(" + amount + ", 0);";
                    break;
                default:
                    script = "this.scrollBy(0, " + amount + ");";
                    break;
            }

            context.RunJs(script);
        }

        internal static void SetInnerHtml(CdpElement element, string html)
        {
            var serializer = new CdpJsonSerializer();
            element.Context.RunJs(string.Format(CdpElementScripts.SetInnerHtmlTemplate, serializer.Serialize(html ?? string.Empty)));
        }

        internal static void SetStyle(CdpElement element, string name, string value)
        {
            var serializer = new CdpJsonSerializer();
            element.Context.RunJs(string.Format(
                CdpElementScripts.SetStyleTemplate,
                serializer.Serialize(name ?? string.Empty),
                serializer.Serialize(value ?? string.Empty)));
        }

        internal static void SetAttr(CdpElement element, string name, string value)
        {
            var serializer = new CdpJsonSerializer();
            element.Context.RunJs(string.Format(
                CdpElementScripts.SetAttrTemplate,
                serializer.Serialize(name ?? string.Empty),
                serializer.Serialize(value ?? string.Empty)));
        }

        internal static void SetValue(CdpElement element, string value)
        {
            var serializer = new CdpJsonSerializer();
            element.Context.RunJs(CdpElementScripts.BuildSetValueJs(serializer.Serialize(value ?? string.Empty)));
        }

        internal static void SetProperty(CdpElement element, string name, string value)
        {
            var propertyName = CdpElementPropertyHelper.NormalizePropertyName(name);
            var serializer = new CdpJsonSerializer();
            var serialized = serializer.Serialize(value ?? string.Empty);

            if (string.Equals(propertyName, "value", StringComparison.Ordinal))
            {
                element.Context.RunJs(CdpElementScripts.BuildSetValueJs(serialized));
                return;
            }

            if (string.Equals(propertyName, "innerHTML", StringComparison.Ordinal))
            {
                element.Context.RunJs(string.Format(CdpElementScripts.SetInnerHtmlTemplate, serialized));
                return;
            }

            element.Context.RunJs(string.Format(CdpElementScripts.SetPropertyTemplate, propertyName, serialized));
        }

        internal static void RemoveAttr(CdpElement element, string name)
        {
            var serializer = new CdpJsonSerializer();
            element.Context.RunJs(string.Format(CdpElementScripts.RemoveAttrTemplate, serializer.Serialize(name ?? string.Empty)));
        }

        internal static void ClickToUpload(
            CdpElement element,
            IEnumerable<string> filePaths,
            CdpMouseButton button,
            CdpInteractionMethod method,
            int count)
        {
            var files = NormalizeFiles(filePaths);
            element.Context.Send("DOM.setFileInputFiles", new Dictionary<string, object>
            {
                { "files", files.ToArray() },
                { "backendNodeId", element.BackendNodeId }
            });
            // Do not click file inputs after setFileInputFiles â€?that opens the native file dialog and blocks automation.
            element.Context.RunJs(
                "this.dispatchEvent(new Event('input',{bubbles:true}));this.dispatchEvent(new Event('change',{bubbles:true}));");
        }

        internal static CdpDownloadResult ClickToDownload(
            CdpElement element,
            string savePath,
            string rename,
            string suffix,
            bool newTab,
            CdpLocalFileExistsAction localFileExists,
            CdpMouseButton button,
            CdpInteractionMethod method,
            int count,
            int timeoutMs)
        {
            var manager = element.Tab.Browser.GetDownloadManager();
            var path = string.IsNullOrWhiteSpace(savePath) ? Environment.CurrentDirectory : savePath;
            manager.BeginExpectDownload(element.Tab.Id, path, rename, suffix, localFileExists);
            try
            {
                Click(element, button, method, count);
                return manager.WaitForCompletion(timeoutMs);
            }
            finally
            {
                manager.EndExpectDownload();
            }
        }

        internal static CdpTab ClickForNewTab(
            CdpElement element,
            CdpMouseButton button,
            CdpInteractionMethod method,
            int count,
            int timeoutMs)
        {
            var before = new HashSet<string>(element.Tab.Browser.TabIds);
            var context = element.Context;
            context.PrepareForAction();

            if (method == CdpInteractionMethod.Js)
            {
                context.RunJs(CdpElementScripts.ClickForNewTabJs);
            }
            else
            {
                Click(element, button, method, count);
            }

            return element.Tab.Browser.WaitForNewTab(before, timeoutMs);
        }

        private static bool IsFileInput(CdpElement element)
        {
            return string.Equals(element.Tag, "input", StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(element.Attr("type"), "file", StringComparison.OrdinalIgnoreCase);
        }

        private static void SetFileInput(CdpElement element, object value)
        {
            var files = value is string
                ? new[] { (string)value }
                : (value as IEnumerable<string>)?.ToArray() ?? new string[0];
            element.Context.Send("DOM.setFileInputFiles", new Dictionary<string, object>
            {
                { "files", NormalizeFiles(files).ToArray() },
                { "backendNodeId", element.BackendNodeId }
            });
        }

        private static IEnumerable<string> NormalizeFiles(IEnumerable<string> filePaths)
        {
            return (filePaths ?? Enumerable.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => Path.GetFullPath(path));
        }

        private static string ConvertInputValue(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            var list = value as IEnumerable<string>;
            if (list != null)
            {
                return string.Concat(list.Select(item => item ?? string.Empty));
            }

            return Convert.ToString(value);
        }
    }
}
