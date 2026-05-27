using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace F2B.Browser.IExplore.COM
{
    public sealed class IEWindowController
    {
        private const string EmbeddedFullName = "embedded-mshtml";
        private const string IeServerClassName = "Internet Explorer_Server";
        private const bool EnableDebugLog = false;
        private static readonly Guid IHTMLDocument2Guid = new Guid("332C4425-26CB-11D0-B483-00C04FD90119");
        private static readonly Guid IDispatchGuid = new Guid("00020400-0000-0000-C000-000000000046");
        private static readonly uint WmHtmlGetObject = NativeMethods.RegisterWindowMessage("WM_HTML_GETOBJECT");

        private EmbeddedIEComWindow _window;

        private IEWindowController(EmbeddedIEComWindow window)
        {
            if (window == null)
                throw new ArgumentNullException(nameof(window));

            _window = window;
        }

        public EmbeddedIEComWindow raw
        {
            get { return _window; }
        }

        public long? hwnd
        {
            get { return _window.HWnd == IntPtr.Zero ? (long?)null : _window.HWnd.ToInt64(); }
        }

        public long? doc_hwnd
        {
            get { return _window.DocHWND == IntPtr.Zero ? (long?)null : _window.DocHWND.ToInt64(); }
        }

        public string title
        {
            get
            {
                var document = document_object();
                if (document != null)
                    return SafeToString(ReadDynamicProperty(document, "title"));

                return SafeToString(_window.TopTitle);
            }
        }

        public string url
        {
            get { return SafeToString(_window.LocationURL); }
        }

        public string full_name
        {
            get { return EmbeddedFullName; }
        }

        public static IEWindowController connect_embedded_ie_window(
            string title = null,
            string title_re = null,
            long? hwnd = null,
            int timeout = 60000,
            int interval = 500)
        {
            var timeoutMs = ToSleepMilliseconds(timeout);
            var intervalMs = ToSleepMilliseconds(interval, 50);
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

            while (DateTime.UtcNow <= deadline)
            {
                foreach (var topHwnd in IterTopLevelWindows(title, title_re, hwnd))
                {
                    foreach (var docHwnd in IterEmbeddedIeDocuments(topHwnd))
                    {
                        var window = CreateEmbeddedWindow(topHwnd, docHwnd);
                        if (window == null)
                            continue;

                        var controller = new IEWindowController(window);
                        return controller.wait_ready(timeout: Math.Max(0, interval));
                    }
                }

                if (DateTime.UtcNow >= deadline)
                    break;

                Thread.Sleep(intervalMs);
            }

            throw new InvalidOperationException(
                string.Format(
                    "未找到匹配的嵌入式 IE 窗口: title={0}, title_re={1}, hwnd={2}",
                    FormatValue(title),
                    FormatValue(title_re),
                    hwnd.HasValue ? hwnd.Value.ToString() : "null"));
        }
        public static void diagnose_embedded_ie_window(
            string title = null,
            string title_re = null,
            long? hwnd = null,
            string frame_name = null,
            string input_name = "userID",
            string input_tag = "input")
        {
            Console.WriteLine("========= IE Diagnose Start =========");
            Console.WriteLine("Filter: title=" + FormatValue(title)
                + ", title_re=" + FormatValue(title_re)
                + ", hwnd=" + (hwnd.HasValue ? hwnd.Value.ToString() : "null")
                + ", frame_name=" + FormatValue(frame_name)
                + ", input_name=" + FormatValue(input_name)
                + ", input_tag=" + FormatValue(input_tag));

            var tops = new List<IntPtr>(IterTopLevelWindows(title, title_re, hwnd));
            Console.WriteLine("Matched top windows: " + tops.Count);

            var elementLocator = new Dictionary<string, object>
            {
                { "name", input_name },
                { "tag", string.IsNullOrWhiteSpace(input_tag) ? "input" : input_tag }
            };

            for (var i = 0; i < tops.Count; i++)
            {
                var top = tops[i];
                Console.WriteLine("- Top[" + i + "] HWnd=0x" + top.ToInt64().ToString("x")
                    + " title=" + SafeGetWindowText(top)
                    + " class=" + SafeGetClassName(top));

                var docs = new List<IntPtr>(IterEmbeddedIeDocuments(top));
                Console.WriteLine("  IE_Server docs: " + docs.Count);

                for (var j = 0; j < docs.Count; j++)
                {
                    var docHwnd = docs[j];
                    var window = CreateEmbeddedWindow(top, docHwnd);
                    if (window == null)
                    {
                        Console.WriteLine("    - Doc[" + j + "] HWnd=0x" + docHwnd.ToInt64().ToString("x") + " create window failed");
                        continue;
                    }

                    var rawDoc = ReadDynamicProperty(window, "Document") ?? window.refresh_document();
                    if (rawDoc == null)
                    {
                        Console.WriteLine("    - Doc[" + j + "] HWnd=0x" + docHwnd.ToInt64().ToString("x") + " document=null");
                        continue;
                    }

                    var topDoc = PromoteToTopDocument(rawDoc) ?? rawDoc;
                    var url = SafeToString(ReadDynamicProperty(topDoc, "url"));
                    var ready = SafeToString(ReadDynamicProperty(topDoc, "readyState"));
                    var titleText = SafeToString(ReadDynamicProperty(topDoc, "title"));
                    Console.WriteLine("    - Doc[" + j + "] HWnd=0x" + docHwnd.ToInt64().ToString("x")
                        + " ready=\"" + ready + "\""
                        + " title=\"" + titleText + "\""
                        + " url=\"" + url + "\"");

                    var frameNames = ListFrameNames(topDoc);
                    Console.WriteLine("    frames(" + frameNames.Count + ")=" + string.Join(", ", frameNames));

                    var topMatches = new List<object>(LocateElements(topDoc, elementLocator)).Count;
                    Console.WriteLine("    topDoc locate(name=\"" + input_name + "\", tag=\"" + SafeToString(elementLocator["tag"]) + "\") => " + topMatches);

                    if (!string.IsNullOrWhiteSpace(frame_name))
                    {
                        try
                        {
                            var frameDoc = ResolveFrameDocument(topDoc, new object[] { frame_name });
                            var frameMatches = new List<object>(LocateElements(frameDoc, elementLocator)).Count;
                            Console.WriteLine("    frame \"" + frame_name + "\" resolve=OK, locate => " + frameMatches);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("    frame \"" + frame_name + "\" resolve=FAIL: " + ex.Message);
                        }
                    }
                }
            }

            Console.WriteLine("========= IE Diagnose End =========");
        }

        public IEWindowController wait_ready(int timeout = 60000, int interval = 200)
        {
            return ExecuteOnStaIfNeeded(() =>
            {
                var timeoutMs = ToSleepMilliseconds(timeout);
                var intervalMs = ToSleepMilliseconds(interval, 50);
                var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

                while (DateTime.UtcNow <= deadline)
                {
                    try
                    {
                        var document = document_object();
                        if (document != null)
                        {
                            var readyState = SafeToString(ReadDynamicProperty(document, "readyState"));
                            if (IsDocumentReady(readyState))
                                return this;
                        }
                    }
                    catch
                    {
                        // ignore and retry until timeout
                    }

                    if (DateTime.UtcNow >= deadline)
                        break;

                    Thread.Sleep(intervalMs);
                }

                return this;
            });
        }

        public object document(IEnumerable<object> frame_path = null)
        {
            return ExecuteOnStaIfNeeded(() =>
            {
                var document = ReadDynamicProperty(_window, "Document");
                if (document == null)
                    document = refresh_document();
                if (document == null)
                    throw new InvalidOperationException("无法获取 IE Document");

                try
                {
                    return ResolveFrameDocument(document, frame_path);
                }
                catch (Exception ex)
                {
                    if (!IsRetryableFrameError(ex))
                        throw;

                    if (frame_path != null)
                    {
                        var switched = TrySwitchToDocumentForFramePath(frame_path, out var resolved);
                        if (switched)
                            return resolved;
                    }

                    var refreshed = refresh_document();
                    if (refreshed == null)
                        throw;

                    try
                    {
                        return ResolveFrameDocument(refreshed, frame_path);
                    }
                    catch (Exception retryEx)
                    {
                        if (frame_path != null)
                        {
                            var switched = TrySwitchToDocumentForFramePath(frame_path, out var resolved);
                            if (switched)
                                return resolved;
                        }

                        throw new InvalidOperationException(retryEx.Message, retryEx);
                    }
                }
            });
        }

        private bool TrySwitchToDocumentForFramePath(IEnumerable<object> framePath, out object resolved)
        {
            resolved = null;
            var top = _window.HWnd;
            if (top == IntPtr.Zero)
                return false;

            foreach (var candidateDocHwnd in IterEmbeddedIeDocuments(top))
            {
                var candidateWindow = CreateEmbeddedWindow(top, candidateDocHwnd);
                if (candidateWindow == null)
                    continue;

                var candidateDocument = ReadDynamicProperty(candidateWindow, "Document")
                    ?? candidateWindow.refresh_document();
                if (candidateDocument == null)
                    continue;

                try
                {
                    var candidateResolved = ResolveFrameDocument(candidateDocument, framePath);
                    _window = candidateWindow;
                    resolved = candidateResolved;
                    return true;
                }
                catch
                {
                    // continue probing next candidate document
                }
            }

            return false;
        }

        private bool TrySwitchToDocumentForLocator(IDictionary<string, object> locator, out object resolved)
        {
            resolved = null;
            var top = _window.HWnd;
            if (top == IntPtr.Zero)
                return false;

            foreach (var candidateDocHwnd in IterEmbeddedIeDocuments(top))
            {
                if (candidateDocHwnd == _window.DocHWND)
                    continue;

                var candidateWindow = CreateEmbeddedWindow(top, candidateDocHwnd);
                if (candidateWindow == null)
                    continue;

                var candidateDocument = ReadDynamicProperty(candidateWindow, "Document")
                    ?? candidateWindow.refresh_document();
                if (candidateDocument == null)
                    continue;

                var topDocument = PromoteToTopDocument(candidateDocument) ?? candidateDocument;

                var candidateMatches = new List<object>(LocateElements(candidateDocument, locator));
                if (candidateMatches.Count == 0)
                    candidateMatches = new List<object>(LocateElements(topDocument, locator));

                if (candidateMatches.Count == 0)
                    continue;

                _window = candidateWindow;
                resolved = topDocument;
                return true;
            }

            return false;
        }

        private bool TryRebindCurrentDocument(out object resolved)
        {
            resolved = null;

            var top = _window.HWnd;
            var doc = _window.DocHWND;
            if (top == IntPtr.Zero || doc == IntPtr.Zero)
                return false;

            var reboundWindow = CreateEmbeddedWindow(top, doc);
            if (reboundWindow == null)
                return false;

            var reboundDocument = ReadDynamicProperty(reboundWindow, "Document")
                ?? reboundWindow.refresh_document();
            if (reboundDocument == null)
                return false;

            _window = reboundWindow;
            resolved = PromoteToTopDocument(reboundDocument) ?? reboundDocument;
            return true;
        }

        private static bool IsFramePathEmpty(IEnumerable<object> framePath)
        {
            if (framePath == null)
                return true;

            foreach (var _ in framePath)
                return false;

            return true;
        }

        public object refresh_document()
        {
            return ExecuteOnStaIfNeeded(() =>
            {
                var refreshMethod = ReadDynamicProperty(_window, "refresh_document") as Delegate;
                if (refreshMethod != null)
                    return SafeRead(() => refreshMethod.DynamicInvoke());

                return _window.refresh_document();
            });
        }

        public IEWindowController refresh(int timeout = 60000, int interval = 200)
        {
            refresh_document();
            return wait_ready(timeout, interval);
        }

        public bool has_frame(IEnumerable<object> frame_path)
        {
            return ExecuteOnStaIfNeeded(() =>
            {
                try
                {
                    document(frame_path);
                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }

        public IEWindowController wait_for_frame(
            IEnumerable<object> frame_path,
            int timeout = 60000,
            int interval = 200)
        {
            return ExecuteOnStaIfNeeded(() =>
            {
                var deadline = DateTime.UtcNow.AddMilliseconds(ToSleepMilliseconds(timeout));
                var targetFramePath = frame_path == null ? new List<object>() : new List<object>(frame_path);
                var intervalMs = ToSleepMilliseconds(interval, 50);

                while (DateTime.UtcNow <= deadline)
                {
                    refresh_document();
                    try
                    {
                        document(targetFramePath);
                        return this;
                    }
                    catch (Exception ex)
                    {
                        if (!IsRetryableFrameError(ex))
                            throw;
                    }

                    if (DateTime.UtcNow >= deadline)
                        break;

                    Thread.Sleep(intervalMs);
                }

                throw new InvalidOperationException(
                    string.Format("等待 frame 超时: frame_path={0}", DescribeFramePath(targetFramePath)));
            });
        }
        public IEDomElement[] find_elements(IDictionary<string, object> locator, IEnumerable<object> frame_path = null)
        {
            return ExecuteOnStaIfNeeded(() =>
            {
                if (locator == null)
                    throw new ArgumentNullException(nameof(locator));

                var currentDocument = document(frame_path);
                var rawMatches = new List<object>(LocateElements(currentDocument, locator));

                if (rawMatches.Count == 0 && IsFramePathEmpty(frame_path))
                {
                    if (TryRebindCurrentDocument(out var reboundDocument))
                    {
                        currentDocument = reboundDocument;
                        rawMatches = new List<object>(LocateElements(currentDocument, locator));
                        DebugLog(
                            "find_elements",
                            "rebuilt current COM binding locator={0}, matches={1}",
                            DescribeLocator(locator),
                            rawMatches.Count);
                    }
                }

                if (rawMatches.Count == 0 && IsFramePathEmpty(frame_path))
                {
                    var topDocument = PromoteToTopDocument(currentDocument) ?? currentDocument;
                    if (!ReferenceEquals(topDocument, currentDocument))
                    {
                        var topMatches = new List<object>(LocateElements(topDocument, locator));
                        if (topMatches.Count > 0)
                        {
                            currentDocument = topDocument;
                            rawMatches = topMatches;
                            DebugLog(
                                "find_elements",
                                "matched on top document fallback locator={0}, matches={1}",
                                DescribeLocator(locator),
                                rawMatches.Count);
                        }
                    }
                }

                if (rawMatches.Count == 0 && IsFramePathEmpty(frame_path))
                {
                    if (TrySwitchToDocumentForLocator(locator, out var switchedDocument))
                    {
                        currentDocument = switchedDocument;
                        rawMatches = new List<object>(LocateElements(currentDocument, locator));
                        DebugLog(
                            "find_elements",
                            "switched embedded document by locator={0}, new_doc_hwnd=0x{1}",
                            DescribeLocator(locator),
                            _window.DocHWND.ToInt64().ToString("x"));
                    }
                }

                var elements = new List<IEDomElement>();
                foreach (var element in rawMatches)
                    elements.Add(new IEDomElement(this, element, currentDocument));

                return elements.ToArray();
            });
        }

        public IEDomElement find_element(
            IDictionary<string, object> locator,
            IEnumerable<object> frame_path = null,
            int timeout = 15000,
            int interval = 200)
        {
            return ExecuteOnStaIfNeeded(() =>
            {
                if (locator == null)
                    throw new ArgumentNullException(nameof(locator));

                var deadline = DateTime.UtcNow.AddMilliseconds(ToSleepMilliseconds(timeout));
                Exception lastError = null;

                while (true)
                {
                    try
                    {
                        var matches = find_elements(locator, frame_path);
                        DebugLog(
                            "find_element",
                            "locator={0}, frame_path={1}, matches={2}",
                            DescribeLocator(locator),
                            DescribeFramePath(frame_path),
                            matches == null ? 0 : matches.Length);

                        if (matches.Length > 0)
                        {
                            var preferred = PickPreferredElement(matches);
                            if (preferred != null)
                            {
                                DebugLog("find_element", "selected(preferred): {0}", DescribeDomElement(preferred));
                                return preferred;
                            }

                            DebugLog("find_element", "selected(first): {0}", DescribeDomElement(matches[0]));
                            return matches[0];
                        }
                    }
                    catch (Exception ex)
                    {
                        if (IsRetryableFrameError(ex))
                        {
                            lastError = ex;
                            if (DateTime.UtcNow >= deadline)
                                break;

                            refresh_document();
                            Thread.Sleep(ToSleepMilliseconds(interval, 50));
                            continue;
                        }

                        throw;
                    }

                    if (DateTime.UtcNow >= deadline)
                        break;

                    Thread.Sleep(ToSleepMilliseconds(interval, 50));
                }

                if (lastError != null)
                {
                    var diagnose = BuildNotFoundDiagnostics(locator, frame_path);
                    throw new InvalidOperationException(
                        string.Format(
                            "定位元素前等待 frame 超时: locator={0}, frame_path={1}, error={2}, diagnose={3}",
                            DescribeLocator(locator),
                            DescribeFramePath(frame_path),
                            lastError.Message,
                            diagnose),
                        lastError);
                }

                var notFoundDiagnose = BuildNotFoundDiagnostics(locator, frame_path);
                throw new InvalidOperationException(
                    string.Format(
                        "未找到元素: locator={0}, frame_path={1}, diagnose={2}",
                        DescribeLocator(locator),
                        DescribeFramePath(frame_path),
                        notFoundDiagnose));
            });
        }

        private static IEDomElement PickPreferredElement(IEnumerable<IEDomElement> elements)
        {
            if (elements == null)
                return null;

            IEDomElement first = null;
            foreach (var element in elements)
            {
                if (element == null)
                    continue;

                if (first == null)
                    first = element;

                if (IsElementInteractable(element.raw))
                    return element;
            }

            return first;
        }

        private static bool IsElementInteractable(object element)
        {
            if (element == null)
                return false;

            var disabled = SafeRead(() => ReadDynamicProperty(element, "disabled"));
            if (disabled is bool && (bool)disabled)
                return false;

            var type = SafeToString(SafeRead(() => ReadDynamicProperty(element, "type")));
            if (string.Equals(type, "hidden", StringComparison.OrdinalIgnoreCase))
                return false;

            var tag = SafeToString(SafeRead(() => ReadDynamicProperty(element, "tagName")));
            if (string.Equals(tag, "input", StringComparison.OrdinalIgnoreCase)
                && string.Equals(type, "hidden", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var offsetWidth = SafeInt(SafeRead(() => ReadDynamicProperty(element, "offsetWidth")));
            var offsetHeight = SafeInt(SafeRead(() => ReadDynamicProperty(element, "offsetHeight")));
            if (offsetWidth.HasValue && offsetHeight.HasValue && offsetWidth.Value <= 0 && offsetHeight.Value <= 0)
                return false;

            var style = SafeRead(() => ReadDynamicProperty(element, "style"));
            var display = SafeToString(SafeRead(() => ReadDynamicProperty(style, "display")));
            if (string.Equals(display, "none", StringComparison.OrdinalIgnoreCase))
                return false;

            var visibility = SafeToString(SafeRead(() => ReadDynamicProperty(style, "visibility")));
            if (string.Equals(visibility, "hidden", StringComparison.OrdinalIgnoreCase)
                || string.Equals(visibility, "collapse", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        public void input_text(
            IDictionary<string, object> locator,
            object value,
            IEnumerable<object> frame_path = null,
            int timeout = 15000,
            bool trigger_events = true,
            int delay_before = 0,
            int delay_after = 0)
        {
            ExecuteOnStaIfNeeded(() =>
            {
                DebugLog("input_text", "begin locator={0}, frame_path={1}, value={2}", DescribeLocator(locator), DescribeFramePath(frame_path), FormatAny(value));
                var element = find_element(locator, frame_path, timeout);
                element.set_value(value, trigger_events, delay_before, delay_after);
                DebugLog("input_text", "completed element={0}", DescribeDomElement(element));
                return true;
            });
        }

        public void click_element_native(
            IDictionary<string, object> locator,
            IEnumerable<object> frame_path = null,
            int timeout = 15000,
            int delay_before = 0,
            int delay_after = 0)
        {
            ExecuteOnStaIfNeeded(() =>
            {
                var element = find_element(locator, frame_path, timeout);
                element.native_click(delay_before, delay_after);
                return true;
            });
        }

        public void select_option(
            IDictionary<string, object> locator,
            IEnumerable<object> frame_path = null,
            int timeout = 15000,
            string text = null,
            string value = null,
            int? index = null,
            string text_contains = null,
            string text_re = null,
            bool trigger_events = true,
            bool trigger_dblclick = false,
            int delay_before = 0,
            int delay_after = 0)
        {
            ExecuteOnStaIfNeeded(() =>
            {
                var element = find_element(locator, frame_path, timeout);
                element.select_option(
                    text,
                    value,
                    index,
                    text_contains,
                    text_re,
                    trigger_events,
                    trigger_dblclick,
                    delay_before,
                    delay_after);
                return true;
            });
        }

        public void check_element(
            IDictionary<string, object> locator,
            IEnumerable<object> frame_path = null,
            int timeout = 15000,
            bool trigger_events = true,
            int delay_before = 0,
            int delay_after = 0)
        {
            ExecuteOnStaIfNeeded(() =>
            {
                var element = find_element(locator, frame_path, timeout);
                element.check(trigger_events, delay_before, delay_after);
                return true;
            });
        }

        public void uncheck_element(
            IDictionary<string, object> locator,
            IEnumerable<object> frame_path = null,
            int timeout = 15000,
            bool trigger_events = true,
            int delay_before = 0,
            int delay_after = 0)
        {
            ExecuteOnStaIfNeeded(() =>
            {
                var element = find_element(locator, frame_path, timeout);
                element.uncheck(trigger_events, delay_before, delay_after);
                return true;
            });
        }

        public string get_element_text(
            IDictionary<string, object> locator,
            IEnumerable<object> frame_path = null,
            int timeout = 15000)
        {
            return ExecuteOnStaIfNeeded(() =>
            {
                var element = find_element(locator, frame_path, timeout);
                return element.get_text();
            });
        }

        public object get_element_attribute(
            IDictionary<string, object> locator,
            string name,
            IEnumerable<object> frame_path = null,
            int timeout = 15000,
            object default_value = null)
        {
            return ExecuteOnStaIfNeeded(() =>
            {
                var element = find_element(locator, frame_path, timeout);
                return element.get_attribute(name, default_value);
            });
        }

        public void set_attribute(
            IDictionary<string, object> locator,
            string name,
            object value,
            IEnumerable<object> frame_path = null,
            int timeout = 15000,
            bool trigger_events = true,
            int delay_before = 0,
            int delay_after = 0)
        {
            ExecuteOnStaIfNeeded(() =>
            {
                var element = find_element(locator, frame_path, timeout);
                element.set_attribute(name, value, trigger_events, delay_before, delay_after);
                return true;
            });
        }

        public object run_js(string script, IEnumerable<object> frame_path = null)
        {
            return run_js(script, frame_path, null);
        }

        public object run_js(string script, IEnumerable<object> frame_path = null, IList<object> args = null)
        {
            return ExecuteOnStaIfNeeded(() =>
            {
                if (string.IsNullOrWhiteSpace(script))
                    return null;
                if (frame_path == null)
                {
                    frame_path = new List<object>();
                }
                var currentDocument = document(frame_path);
                var window = ReadDynamicProperty(currentDocument, "parentWindow");
                if (window == null)
                    throw new InvalidOperationException("无法获取 document.parentWindow，无法执行 JS");

                if (args != null)
                {
                    var argsLiteral = ToJavaScriptLiteral(args);
                    var functionCallScript = "(function(){var __fn=(" + script + ");"
                        + "if(typeof __fn!=='function'){throw new Error('run_js: script must evaluate to a function when args is provided');}"
                        + "return __fn.apply(window," + argsLiteral + ");})();";

                    try
                    {
                        return InvokeDynamicMethod(window, "eval", functionCallScript);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("run_js(函数+参数) 执行失败: " + ex.Message, ex);
                    }
                }

                var result = SafeRead(() => InvokeDynamicMethod(window, "eval", script));
                if (result != null)
                    return result;

                return SafeRead(() => InvokeDynamicMethod(window, "execScript", script, "javascript"));
            });
        }

        public IEDomElement[] get_elements(IDictionary<string, object> locator, IEnumerable<object> frame_path = null)
        {
            return find_elements(locator, frame_path);
        }

        public void press_keys(
            IDictionary<string, object> locator,
            object keys,
            IEnumerable<object> frame_path = null,
            int timeout = 15000,
            int delay_before = 0,
            int delay_after = 0)
        {
            ExecuteOnStaIfNeeded(() =>
            {
                var element = find_element(locator, frame_path, timeout);
                element.press_keys(keys, delay_before, delay_after);
                return true;
            });
        }

        public void double_click_element(
            IDictionary<string, object> locator,
            IEnumerable<object> frame_path = null,
            int timeout = 15000,
            int delay_before = 0,
            int delay_after = 0)
        {
            ExecuteOnStaIfNeeded(() =>
            {
                var element = find_element(locator, frame_path, timeout);
                element.double_click(delay_before, delay_after);
                return true;
            });
        }

        public IEDomElement get_parent(
            IDictionary<string, object> locator,
            IEnumerable<object> frame_path = null,
            int timeout = 15000)
        {
            var element = find_element(locator, frame_path, timeout);
            return element.get_parent();
        }

        public IEDomElement[] get_children(
            IDictionary<string, object> locator,
            IEnumerable<object> frame_path = null,
            int timeout = 15000)
        {
            var element = find_element(locator, frame_path, timeout);
            return element.get_children();
        }

        public bool is_checked(
            IDictionary<string, object> locator,
            IEnumerable<object> frame_path = null,
            int timeout = 0)
        {
            return ExecuteOnStaIfNeeded(() =>
            {
                var element = find_element(locator, frame_path, timeout);
                return element.is_checked();
            });
        }

        public string[] get_selected_options(
            IDictionary<string, object> locator,
            IEnumerable<object> frame_path = null,
            int timeout = 15000)
        {
            return ExecuteOnStaIfNeeded(() =>
            {
                var element = find_element(locator, frame_path, timeout);
                return element.get_selected_options();
            });
        }

        public object document_object()
        {
            return ExecuteOnStaIfNeeded(() => _window.refresh_document());
        }
        private static T ExecuteOnStaIfNeeded<T>(Func<T> operation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
                return operation();

            T result = default(T);
            Exception error = null;
            using (var done = new ManualResetEvent(false))
            {
                var thread = new Thread(() =>
                {
                    try
                    {
                        result = operation();
                    }
                    catch (Exception ex)
                    {
                        error = ex;
                    }
                    finally
                    {
                        done.Set();
                    }
                });

                thread.IsBackground = true;
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                done.WaitOne();

                if (error != null)
                    ExceptionDispatchInfo.Capture(error).Throw();

                return result;
            }
        }

        private static object ResolveFrameDocument(object document, IEnumerable<object> framePath)
        {
            var path = framePath == null ? new List<object>() : new List<object>(framePath);
            var current = PromoteToTopDocument(document);
            if (path.Count == 0)
                return current ?? document;

            foreach (var frameRef in path)
            {
                var frames = GetFrameCollection(current);
                if (frames == null)
                    throw new InvalidOperationException("当前文档不包含 frames: frame_ref=" + FormatAny(frameRef));

                object frameWindow = null;
                frameWindow = TryGetFrameWindowByTypedInterop(frames, frameRef);
                if (frameWindow == null)
                    frameWindow = SafeRead(() => InvokeDynamicMethod(frames, "item", frameRef));

                if (frameWindow == null)
                {
                    var count = GetFrameCount(frames);
                    for (var index = 0; index < count; index++)
                    {
                        var candidate = TryGetFrameWindowByTypedInterop(frames, index)
                            ?? SafeRead(() => InvokeDynamicMethod(frames, "item", index));
                        if (candidate == null)
                            continue;

                        var candidateName = GetFrameWindowName(candidate);
                        if (string.IsNullOrWhiteSpace(candidateName))
                        {
                            var candidateDocument = ReadDynamicProperty(candidate, "document");
                            candidateName = SafeToString(ReadDynamicProperty(candidateDocument, "name"));

                            if (string.IsNullOrWhiteSpace(candidateName))
                            {
                                var frameElement = ReadDynamicProperty(candidateDocument, "frameElement");
                                candidateName = SafeToString(ReadDynamicProperty(frameElement, "name"));
                                if (string.IsNullOrWhiteSpace(candidateName))
                                    candidateName = SafeToString(ReadDynamicProperty(frameElement, "id"));
                            }
                        }

                        if ((frameRef is int && (int)frameRef == index)
                            || string.Equals(SafeToString(frameRef), candidateName, StringComparison.Ordinal)
                            || string.Equals(SafeToString(frameRef), candidateName, StringComparison.OrdinalIgnoreCase))
                        {
                            frameWindow = candidate;
                            break;
                        }
                    }
                }

                if (frameWindow == null)
                    throw new InvalidOperationException("未找到 frame: " + FormatAny(frameRef));

                current = GetFrameWindowDocument(frameWindow) ?? ReadDynamicProperty(frameWindow, "document");
                if (current == null)
                    throw new InvalidOperationException("frame 没有可访问的 document: " + FormatAny(frameRef));
            }

            return current;
        }

        private static object PromoteToTopDocument(object document)
        {
            if (document == null)
                return null;

            try
            {
                var parentWindow = ReadDynamicProperty(document, "parentWindow");
                var topWindow = ReadDynamicProperty(parentWindow, "top");
                var topDocument = ReadDynamicProperty(topWindow, "document");
                if (topDocument != null)
                    return topDocument;
            }
            catch
            {
                // ignore and fallback
            }

            return document;
        }

        private static object GetFrameCollection(object document)
        {
            if (document == null)
                return null;

            var frames = ReadDynamicProperty(document, "frames");
            if (frames != null)
                return frames;

            var parentWindow = ReadDynamicProperty(document, "parentWindow");
            frames = ReadDynamicProperty(parentWindow, "frames");
            if (frames != null)
                return frames;

            var script = ReadDynamicProperty(document, "Script");
            return ReadDynamicProperty(script, "frames");
        }

        private static object TryGetFrameWindowByTypedInterop(object frames, object frameRef)
        {
            return SafeRead(() => InvokeDynamicMethod(frames, "item", frameRef));
        }

        private static int GetFrameCount(object frames)
        {
            return SafeInt(ReadDynamicProperty(frames, "length")) ?? 0;
        }

        private static string GetFrameWindowName(object frameWindow)
        {
            return SafeToString(ReadDynamicProperty(frameWindow, "name"));
        }

        private static object GetFrameWindowDocument(object frameWindow)
        {
            return ReadDynamicProperty(frameWindow, "document");
        }

        private static List<string> ListFrameNames(object document)
        {
            var result = new List<string>();
            var frames = GetFrameCollection(document);
            if (frames == null)
                return result;

            var count = GetFrameCount(frames);
            for (var i = 0; i < count; i++)
            {
                var frameWindow = TryGetFrameWindowByTypedInterop(frames, i)
                    ?? SafeRead(() => InvokeDynamicMethod(frames, "item", i));
                if (frameWindow == null)
                    continue;

                var name = GetFrameWindowName(frameWindow);
                if (string.IsNullOrWhiteSpace(name))
                    name = "(index=" + i + ")";
                result.Add(name);
            }

            return result;
        }

        private static IEnumerable<object> LocateElements(object document, IDictionary<string, object> locator)
        {
            var selector = GetLocatorString(locator, "selector");
            IEnumerable<object> candidates;

            if (!string.IsNullOrWhiteSpace(selector))
            {
                var nodeList = SafeRead(() => InvokeDynamicMethod(document, "querySelectorAll", selector));
                candidates = EnumerateIndexedCollection(nodeList);
            }
            else
            {
                var all = ReadDynamicProperty(document, "all") ?? SafeRead(() => InvokeDynamicMethod(document, "getElementsByTagName", "*"));
                candidates = EnumerateIndexedCollection(all);
            }

            var filtered = new List<object>();
            foreach (var candidate in candidates)
            {
                if (candidate == null)
                    continue;
                if (ElementMatches(candidate, locator))
                    filtered.Add(candidate);
            }

            var index = SafeInt(GetLocatorValue(locator, "index"));
            if (index.HasValue)
            {
                if (index.Value >= 0 && index.Value < filtered.Count)
                    return new[] { filtered[index.Value] };
                return new object[0];
            }

            return filtered;
        }

        private static bool ElementMatches(object element, IDictionary<string, object> locator)
        {
            if (locator == null)
                return false;

            if (!MatchesString(SafeToString(ReadDynamicProperty(element, "id")), GetLocatorString(locator, "id"), false))
                return false;
            if (!MatchesString(SafeToString(ReadDynamicProperty(element, "name")), GetLocatorString(locator, "name"), false))
                return false;
            if (!MatchesString(SafeToString(ReadDynamicProperty(element, "tagName")), GetLocatorString(locator, "tag"), true))
                return false;
            if (!MatchesString(SafeToString(ReadDynamicProperty(element, "type")), GetLocatorString(locator, "type"), true))
                return false;

            var classFilter = GetLocatorString(locator, "class_name") ?? GetLocatorString(locator, "class");
            if (!string.IsNullOrWhiteSpace(classFilter))
            {
                var className = SafeToString(ReadDynamicProperty(element, "className"));
                if (className.IndexOf(classFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }

            var text = NormalizeText(SafeToString(ReadDynamicProperty(element, "innerText")));
            if (!MatchesString(text, GetLocatorString(locator, "text"), false))
                return false;

            var textContains = GetLocatorString(locator, "text_contains");
            if (!string.IsNullOrWhiteSpace(textContains)
                && text.IndexOf(textContains, StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            var textRegex = GetLocatorString(locator, "text_re");
            if (!string.IsNullOrWhiteSpace(textRegex) && !Regex.IsMatch(text, textRegex, RegexOptions.IgnoreCase))
                return false;

            if (!MatchesValue(element, GetLocatorString(locator, "value")))
                return false;

            var attrs = GetLocatorDictionary(locator, "attrs");
            if (attrs != null)
            {
                foreach (var pair in attrs)
                {
                    var attrValue = SafeToString(SafeRead(() => InvokeDynamicMethod(element, "getAttribute", pair.Key)));
                    if (!string.Equals(attrValue, SafeToString(pair.Value), StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }

            if (!MatchesGenericLocatorAttributes(element, locator))
                return false;

            return true;
        }

        private static bool MatchesGenericLocatorAttributes(object element, IDictionary<string, object> locator)
        {
            if (element == null || locator == null)
                return false;

            foreach (var pair in locator)
            {
                if (pair.Key == null)
                    continue;

                if (IsReservedLocatorKey(pair.Key))
                    continue;

                var expected = SafeToString(pair.Value);
                if (string.IsNullOrWhiteSpace(expected))
                    continue;

                var actual = SafeToString(SafeRead(() => InvokeDynamicMethod(element, "getAttribute", pair.Key)));
                if (string.IsNullOrWhiteSpace(actual))
                    actual = SafeToString(SafeRead(() => ReadDynamicProperty(element, pair.Key)));

                if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        private static bool IsReservedLocatorKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return true;

            return string.Equals(key, "selector", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "index", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "id", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "name", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "tag", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "type", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "class", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "class_name", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "text", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "text_contains", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "text_re", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "value", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "attrs", StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesValue(object element, string expected)
        {
            if (string.IsNullOrWhiteSpace(expected))
                return true;

            var expectedNormalized = NormalizeText(expected);
            if (string.IsNullOrWhiteSpace(expectedNormalized))
                return true;

            var value = NormalizeText(SafeToString(ReadDynamicProperty(element, "value")));
            if (string.Equals(value, expectedNormalized, StringComparison.OrdinalIgnoreCase))
                return true;

            var attrValue = NormalizeText(SafeToString(SafeRead(() => InvokeDynamicMethod(element, "getAttribute", "value"))));
            if (string.Equals(attrValue, expectedNormalized, StringComparison.OrdinalIgnoreCase))
                return true;

            var text = NormalizeText(SafeToString(ReadDynamicProperty(element, "innerText")));
            if (string.Equals(text, expectedNormalized, StringComparison.OrdinalIgnoreCase))
                return true;

            var textContent = NormalizeText(SafeToString(ReadDynamicProperty(element, "textContent")));
            return string.Equals(textContent, expectedNormalized, StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<object> EnumerateIndexedCollection(object collection)
        {
            if (collection == null)
                yield break;

            var count = SafeInt(ReadDynamicProperty(collection, "length"));
            if (count.HasValue)
            {
                for (var i = 0; i < count.Value; i++)
                {
                    object item = null;
                    try
                    {
                        item = InvokeDynamicMethod(collection, "item", i);
                    }
                    catch
                    {
                        item = null;
                    }

                    if (item != null)
                        yield return item;
                }

                yield break;
            }

            var enumerable = collection as IEnumerable;
            if (enumerable == null)
                yield break;

            foreach (var item in enumerable)
                yield return item;
        }

        private static object InvokeDynamicMethod(object instance, string methodName, params object[] args)
        {
            if (instance == null || string.IsNullOrWhiteSpace(methodName))
                return null;

            return instance.GetType().InvokeMember(
                methodName,
                BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance,
                null,
                instance,
                args);
        }

        private static void SetDynamicProperty(object instance, string propertyName, object value)
        {
            if (instance == null || string.IsNullOrWhiteSpace(propertyName))
                return;

            try
            {
                instance.GetType().InvokeMember(
                    propertyName,
                    BindingFlags.SetProperty | BindingFlags.Public | BindingFlags.Instance,
                    null,
                    instance,
                    new[] { value });
            }
            catch
            {
                try
                {
                    instance.GetType().InvokeMember(
                        propertyName,
                        BindingFlags.SetField | BindingFlags.Public | BindingFlags.Instance,
                        null,
                        instance,
                        new[] { value });
                }
                catch
                {
                    // ignore
                }
            }
        }

        private static IEnumerable<IntPtr> IterTopLevelWindows(string title, string titleRegex, long? hwnd)
        {
            Regex titlePattern = null;
            if (!string.IsNullOrWhiteSpace(titleRegex))
                titlePattern = new Regex(titleRegex, RegexOptions.IgnoreCase | RegexOptions.Compiled);

            var matched = new List<IntPtr>();
            NativeMethods.EnumWindows((candidateHwnd, _) =>
            {
                if (!NativeMethods.IsWindowVisible(candidateHwnd))
                    return true;

                if (hwnd.HasValue && candidateHwnd.ToInt64() != hwnd.Value)
                    return true;

                var windowTitle = SafeGetWindowText(candidateHwnd);
                if (title != null && !string.Equals(windowTitle, title, StringComparison.Ordinal))
                    return true;

                if (titlePattern != null && !titlePattern.IsMatch(windowTitle))
                    return true;

                matched.Add(candidateHwnd);
                return true;
            }, IntPtr.Zero);

            return matched;
        }

        private static IEnumerable<IntPtr> IterEmbeddedIeDocuments(IntPtr topHwnd)
        {
            foreach (var candidateHwnd in IterWindowTree(topHwnd))
            {
                object document;
                if (TryGetHtmlDocumentFromHwnd(candidateHwnd, out document))
                    yield return candidateHwnd;
            }
        }

        private static IEnumerable<IntPtr> IterWindowTree(IntPtr topHwnd)
        {
            if (topHwnd == IntPtr.Zero || !NativeMethods.IsWindow(topHwnd))
                yield break;

            yield return topHwnd;

            var descendants = new List<IntPtr>();
            NativeMethods.EnumChildWindows(topHwnd, (childHwnd, _) =>
            {
                descendants.Add(childHwnd);
                return true;
            }, IntPtr.Zero);

            foreach (var childHwnd in descendants)
                yield return childHwnd;
        }

        private static EmbeddedIEComWindow CreateEmbeddedWindow(IntPtr topHwnd, IntPtr docHwnd)
        {
            object document;
            if (!TryGetHtmlDocumentFromHwnd(docHwnd, out document))
                return null;

            return new EmbeddedIEComWindow(
                topHwnd,
                SafeGetWindowText(topHwnd),
                SafeGetClassName(topHwnd),
                docHwnd,
                document);
        }

        private static bool TryGetHtmlDocumentFromHwnd(IntPtr hwnd, out object document)
        {
            document = null;
            if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd))
                return false;

            IntPtr lResult;
            var sent = NativeMethods.SendMessageTimeout(
                hwnd,
                WmHtmlGetObject,
                IntPtr.Zero,
                IntPtr.Zero,
                NativeMethods.SmtoAbortIfHung,
                1000,
                out lResult);

            if (sent == IntPtr.Zero || lResult == IntPtr.Zero)
                return false;

            var iid = IHTMLDocument2Guid;
            object docObject;
            var hr = NativeMethods.ObjectFromLresult(lResult, ref iid, 0, out docObject);
            if (hr != 0 || docObject == null)
            {
                iid = IDispatchGuid;
                hr = NativeMethods.ObjectFromLresult(lResult, ref iid, 0, out docObject);
                if (hr != 0 || docObject == null)
                    return false;
            }

            document = docObject;
            return true;
        }

        private static object ReadDynamicProperty(object instance, string propertyName)
        {
            if (instance == null || string.IsNullOrWhiteSpace(propertyName))
                return null;

            try
            {
                return instance.GetType().InvokeMember(
                    propertyName,
                    BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance,
                    binder: null,
                    target: instance,
                    args: null);
            }
            catch
            {
                try
                {
                    return instance.GetType().InvokeMember(
                        propertyName,
                        BindingFlags.GetField | BindingFlags.Public | BindingFlags.Instance,
                        binder: null,
                        target: instance,
                        args: null);
                }
                catch
                {
                    return null;
                }
            }
        }

        private static object ReadDynamicProperty(object instance, string propertyName, object defaultValue)
        {
            var value = ReadDynamicProperty(instance, propertyName);
            return value ?? defaultValue;
        }

        private static object ReadDynamicNestedProperty(object instance, params string[] propertyNames)
        {
            object current = instance;
            for (int i = 0; i < propertyNames.Length; i++)
            {
                current = ReadDynamicProperty(current, propertyNames[i]);
                if (current == null)
                    return null;
            }

            return current;
        }

        private static bool IsDocumentReady(string readyState)
        {
            if (string.IsNullOrWhiteSpace(readyState))
                return false;

            return readyState.Equals("complete", StringComparison.OrdinalIgnoreCase)
                || readyState.Equals("interactive", StringComparison.OrdinalIgnoreCase);
        }

        private static string SafeGetWindowText(IntPtr hwnd)
        {
            try
            {
                var builder = new StringBuilder(512);
                NativeMethods.GetWindowText(hwnd, builder, builder.Capacity);
                return builder.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string SafeGetClassName(IntPtr hwnd)
        {
            try
            {
                var builder = new StringBuilder(256);
                NativeMethods.GetClassName(hwnd, builder, builder.Capacity);
                return builder.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static object SafeRead(Func<object> action)
        {
            try
            {
                return action();
            }
            catch
            {
                return null;
            }
        }

        private static string SafeToString(object value)
        {
            return value == null ? string.Empty : Convert.ToString(value) ?? string.Empty;
        }

        private static int? SafeInt(object value)
        {
            try
            {
                if (value == null)
                    return null;
                return Convert.ToInt32(value);
            }
            catch
            {
                return null;
            }
        }

        private static string ReadDocumentUrl(object document)
        {
            if (document == null)
                return string.Empty;

            var directUrl = ReadDynamicProperty(document, "url")
                ?? ReadDynamicProperty(document, "URL")
                ?? ReadDynamicNestedProperty(document, "parentWindow", "location", "href")
                ?? ReadDynamicNestedProperty(document, "parentWindow", "location");

            return SafeToString(directUrl);
        }

        private static int ToSleepMilliseconds(int milliseconds, int minimumMilliseconds = 0)
        {
            if (milliseconds < 0)
                milliseconds = 0;

            var normalized = milliseconds;
            if (normalized < minimumMilliseconds)
                normalized = minimumMilliseconds;

            return normalized;
        }

        private static string FormatValue(string value)
        {
            return value == null ? "null" : "\"" + value + "\"";
        }

        private static string FormatAny(object value)
        {
            return value == null ? "null" : "\"" + SafeToString(value) + "\"";
        }

        private static bool IsRetryableFrameError(Exception ex)
        {
            var message = ex == null ? string.Empty : ex.Message ?? string.Empty;
            return message.IndexOf("未找到 frame", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("当前文档不包含 frames", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("frame 没有可访问的 document", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("无法获取 IE Document", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string DescribeFramePath(IEnumerable<object> framePath)
        {
            if (framePath == null)
                return "[]";

            var parts = new List<string>();
            foreach (var item in framePath)
                parts.Add(FormatAny(item));
            return "[" + string.Join(", ", parts) + "]";
        }

        private static string DescribeLocator(IDictionary<string, object> locator)
        {
            if (locator == null || locator.Count == 0)
                return "{}";

            var parts = new List<string>();
            foreach (var pair in locator)
                parts.Add(pair.Key + "=" + FormatAny(pair.Value));
            return "{" + string.Join(", ", parts) + "}";
        }
        private string BuildNotFoundDiagnostics(IDictionary<string, object> locator, IEnumerable<object> framePath)
        {
            try
            {
                var currentDocument = document(framePath);
                var probe = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                var tag = GetLocatorString(locator, "tag");
                if (!string.IsNullOrWhiteSpace(tag))
                    probe["tag"] = tag;

                var type = GetLocatorString(locator, "type");
                if (!string.IsNullOrWhiteSpace(type))
                    probe["type"] = type;

                var candidates = new List<object>(LocateElements(currentDocument, probe));

                if (candidates.Count == 0)
                {
                    var allDocStats = DiagnoseAcrossEmbeddedDocuments(locator, probe);
                    if (!string.IsNullOrWhiteSpace(allDocStats))
                        return "no candidates for tag/type; docs=" + allDocStats;

                    return "no candidates for tag/type";
                }

                var samples = new List<string>();
                for (var i = 0; i < candidates.Count && i < 8; i++)
                {
                    samples.Add(DescribeCandidate(candidates[i], i));
                }

                return string.Format("candidates={0}, sample=[{1}]", candidates.Count, string.Join("; ", samples));
            }
            catch (Exception ex)
            {
                return "diagnose failed: " + ex.Message;
            }
        }


        private string DiagnoseAcrossEmbeddedDocuments(IDictionary<string, object> locator, IDictionary<string, object> probe)
        {
            var top = _window.HWnd;
            if (top == IntPtr.Zero)
                return string.Empty;

            var stats = new List<string>();
            foreach (var candidateDocHwnd in IterEmbeddedIeDocuments(top))
            {
                try
                {
                    var candidateWindow = CreateEmbeddedWindow(top, candidateDocHwnd);
                    if (candidateWindow == null)
                        continue;

                    var candidateDocument = ReadDynamicProperty(candidateWindow, "Document")
                        ?? candidateWindow.refresh_document();
                    if (candidateDocument == null)
                        continue;

                    var topDocument = PromoteToTopDocument(candidateDocument) ?? candidateDocument;

                    var locatorMatches = new List<object>(LocateElements(topDocument, locator)).Count;
                    var probeMatches = new List<object>(LocateElements(topDocument, probe)).Count;

                    var title = SafeToString(ReadDynamicProperty(topDocument, "title"));
                    var url = SafeToString(ReadDynamicProperty(topDocument, "url"));

                    stats.Add(string.Format(
                        "doc=0x{0}, locator={1}, probe={2}, title={3}, url={4}{5}",
                        candidateDocHwnd.ToInt64().ToString("x"),
                        locatorMatches,
                        probeMatches,
                        FormatAny(TruncateDebug(title, 36)),
                        FormatAny(TruncateDebug(url, 48)),
                        candidateDocHwnd == _window.DocHWND ? ", current=1" : string.Empty));
                }
                catch (Exception ex)
                {
                    stats.Add(string.Format(
                        "doc=0x{0}, error={1}",
                        candidateDocHwnd.ToInt64().ToString("x"),
                        FormatAny(TruncateDebug(ex.Message, 48))));
                }
            }

            return string.Join(" | ", stats);
        }

        private static string DescribeDomElement(IEDomElement element)
        {
            if (element == null)
                return "null";

            var raw = element.raw;
            var tag = SafeToString(ReadDynamicProperty(raw, "tagName"));
            var type = SafeToString(ReadDynamicProperty(raw, "type"));
            var id = SafeToString(ReadDynamicProperty(raw, "id"));
            var name = SafeToString(ReadDynamicProperty(raw, "name"));
            var value = SafeToString(ReadDynamicProperty(raw, "value"));

            return string.Format(
                "tag={0}, type={1}, id={2}, name={3}, value={4}, interactable={5}",
                FormatAny(tag),
                FormatAny(type),
                FormatAny(id),
                FormatAny(name),
                FormatAny(TruncateDebug(value, 60)),
                IsElementInteractable(raw));
        }

        private static void DebugLog(string stage, string message, params object[] args)
        {
            if (!EnableDebugLog)
                return;

            string detail;
            try
            {
                detail = string.Format(message ?? string.Empty, args ?? new object[0]);
            }
            catch
            {
                detail = message ?? string.Empty;
            }

            Console.WriteLine("[IE-DEBUG {0:HH:mm:ss.fff}] {1}: {2}", DateTime.Now, stage ?? "stage", detail);
        }

        private static string DescribeCandidate(object element, int index)
        {
            var tag = SafeToString(ReadDynamicProperty(element, "tagName"));
            var type = SafeToString(ReadDynamicProperty(element, "type"));
            var id = SafeToString(ReadDynamicProperty(element, "id"));
            var name = SafeToString(ReadDynamicProperty(element, "name"));
            var value = NormalizeText(SafeToString(ReadDynamicProperty(element, "value")));
            var text = NormalizeText(SafeToString(ReadDynamicProperty(element, "innerText")));
            if (string.IsNullOrWhiteSpace(text))
                text = NormalizeText(SafeToString(ReadDynamicProperty(element, "textContent")));

            return string.Format(
                "#{0}{{tag={1},type={2},id={3},name={4},value={5},text={6},interactable={7}}}",
                index,
                FormatAny(tag),
                FormatAny(type),
                FormatAny(id),
                FormatAny(name),
                FormatAny(TruncateDebug(value, 40)),
                FormatAny(TruncateDebug(text, 40)),
                IsElementInteractable(element));
        }

        private static string TruncateDebug(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;

            return text.Substring(0, maxLength) + "...";
        }

        private static string GetLocatorString(IDictionary<string, object> locator, string key)
        {
            return SafeToString(GetLocatorValue(locator, key));
        }

        private static object GetLocatorValue(IDictionary<string, object> locator, string key)
        {
            if (locator == null || string.IsNullOrWhiteSpace(key))
                return null;

            foreach (var pair in locator)
            {
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                    return pair.Value;
            }

            return null;
        }

        private static IDictionary<string, object> GetLocatorDictionary(IDictionary<string, object> locator, string key)
        {
            return GetLocatorValue(locator, key) as IDictionary<string, object>;
        }

        private static bool MatchesString(string actual, string expected, bool ignoreCase)
        {
            if (string.IsNullOrWhiteSpace(expected))
                return true;

            return string.Equals(
                actual ?? string.Empty,
                expected,
                ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }

        private static string NormalizeText(string value)
        {
            return (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private static string ToJavaScriptLiteral(object value)
        {
            if (value == null)
                return "null";

            var text = value as string;
            if (text != null)
            {
                return "\"" + text
                    .Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\r", "\\r")
                    .Replace("\n", "\\n")
                    .Replace("\t", "\\t") + "\"";
            }

            if (value is bool)
                return (bool)value ? "true" : "false";

            if (value is byte || value is sbyte || value is short || value is ushort
                || value is int || value is uint || value is long || value is ulong
                || value is float || value is double || value is decimal)
                return Convert.ToString(value, CultureInfo.InvariantCulture);

            if (value is DateTime)
            {
                var date = (DateTime)value;
                return "new Date(\"" + date.ToString("o", CultureInfo.InvariantCulture) + "\")";
            }

            var dictionary = value as IDictionary;
            if (dictionary != null)
            {
                var parts = new List<string>();
                foreach (DictionaryEntry pair in dictionary)
                {
                    var key = SafeToString(pair.Key);
                    parts.Add(ToJavaScriptObjectKey(key) + ":" + ToJavaScriptLiteral(pair.Value));
                }

                return "{" + string.Join(",", parts) + "}";
            }

            if (!(value is string))
            {
                var enumerable = value as IEnumerable;
                if (enumerable != null)
                {
                    var items = new List<string>();
                    foreach (var item in enumerable)
                    {
                        items.Add(ToJavaScriptLiteral(item));
                    }
                    return "[" + string.Join(",", items) + "]";
                }
            }

            return ToJavaScriptLiteral(SafeToString(value));
        }

        private static string ToJavaScriptObjectKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return "\"\"";

            if (Regex.IsMatch(key, "^[A-Za-z_$][A-Za-z0-9_$]*$"))
                return key;

            return ToJavaScriptLiteral(key);
        }


        public sealed class EmbeddedIEComWindow
        {
            internal EmbeddedIEComWindow(IntPtr topHwnd, string topTitle, string topClassName, IntPtr docHwnd, object document)
            {
                HWnd = topHwnd;
                Document = document;
                TopTitle = topTitle ?? string.Empty;
                TopClassName = topClassName ?? string.Empty;
                DocHWND = docHwnd;
                FullName = EmbeddedFullName;
                LocationURL = ReadDocumentUrl(document);
            }

            public IntPtr HWnd { get; private set; }
            public object Document { get; private set; }
            public string TopTitle { get; private set; }
            public string TopClassName { get; private set; }
            public IntPtr DocHWND { get; private set; }
            public string FullName { get; private set; }
            public string LocationURL { get; private set; }

            public object refresh_document()
            {
                object document;
                if (DocHWND != IntPtr.Zero && TryGetHtmlDocumentFromHwnd(DocHWND, out document))
                {
                    Update(document);
                    return document;
                }

                foreach (var candidateDocHwnd in IterEmbeddedIeDocuments(HWnd))
                {
                    if (!TryGetHtmlDocumentFromHwnd(candidateDocHwnd, out document))
                        continue;

                    DocHWND = candidateDocHwnd;
                    Update(document);
                    return document;
                }

                return null;
            }

            private void Update(object document)
            {
                Document = document;
                TopTitle = SafeGetWindowText(HWnd);
                TopClassName = SafeGetClassName(HWnd);
                LocationURL = ReadDocumentUrl(document);
            }
        }

        public sealed class IEDomElement
        {
            private readonly IEWindowController _controller;
            private readonly object _element;
            private readonly object _document;

            internal IEDomElement(IEWindowController controller, object element, object document)
            {
                _controller = controller;
                _element = element;
                _document = document;
            }

            public object raw
            {
                get { return _element; }
            }

            public void set_value(
                object value,
                bool trigger_events = true,
                int delay_before = 0,
                int delay_after = 0)
            {
                var text = SafeToString(value);
                DebugLog("set_value", "begin element={0}, value={1}, trigger_events={2}", DescribeSelf(), FormatAny(text), trigger_events);

                if (delay_before > 0)
                    Thread.Sleep(ToSleepMilliseconds(delay_before));

                SafeRead(() => InvokeDynamicMethod(_element, "focus"));
                SetDynamicProperty(_element, "value", text);
                SafeRead(() => InvokeDynamicMethod(_element, "setAttribute", "value", text));

                if (trigger_events)
                    TriggerInputEvents();

                var actualValue = SafeToString(ReadDynamicProperty(_element, "value"));
                if (!IsEquivalentValue(actualValue, text))
                {
                    var attrValue = SafeToString(SafeRead(() => InvokeDynamicMethod(_element, "getAttribute", "value")));
                    if (!IsEquivalentValue(attrValue, text))
                    {
                        DebugLog("set_value", "verify failed element={0}, actual.value={1}, actual.attr={2}", DescribeSelf(), FormatAny(actualValue), FormatAny(attrValue));
                        throw new InvalidOperationException(
                            string.Format(
                                "set_value 未生效: expected={0}, actual.value={1}, actual.attr={2}, element={3}",
                                FormatAny(text),
                                FormatAny(actualValue),
                                FormatAny(attrValue),
                                DescribeSelf()));
                    }
                }

                DebugLog("set_value", "verify success element={0}, actual.value={1}", DescribeSelf(), FormatAny(actualValue));

                if (delay_after > 0)
                    Thread.Sleep(ToSleepMilliseconds(delay_after));
            }

            public void native_click(
                int delay_before = 0,
                int delay_after = 0)
            {
                DebugLog("native_click", "begin element={0}", DescribeSelf());
                if (delay_before > 0)
                    Thread.Sleep(ToSleepMilliseconds(delay_before));

                SafeRead(() => InvokeDynamicMethod(_element, "focus"));

                var clicked = false;
                try
                {
                    InvokeDynamicMethod(_element, "click");
                    clicked = true;
                }
                catch
                {
                    clicked = false;
                }

                if (!clicked)
                {
                    SafeRead(() => InvokeDynamicMethod(_element, "fireEvent", "onclick"));
                    DispatchStandardEvent("click");
                }

                DebugLog("native_click", "completed element={0}, clicked={1}", DescribeSelf(), clicked);

                if (delay_after > 0)
                    Thread.Sleep(ToSleepMilliseconds(delay_after));
            }

            public void select_option(
                string text = null,
                string value = null,
                int? index = null,
                string text_contains = null,
                string text_re = null,
                bool trigger_events = true,
                bool trigger_dblclick = false,
                int delay_before = 0,
                int delay_after = 0)
            {
                if (delay_before > 0)
                    Thread.Sleep(ToSleepMilliseconds(delay_before));

                SafeRead(() => InvokeDynamicMethod(_element, "focus"));

                var options = new List<object>();
                var optionsCollection = ReadDynamicProperty(_element, "options") ?? _element;
                foreach (var option in EnumerateIndexedCollection(optionsCollection))
                    options.Add(option);

                if (options.Count == 0)
                    throw new InvalidOperationException("当前元素不是可选下拉框，或未找到可用 options");

                var targetIndex = ResolveOptionIndex(options, text, value, index, text_contains, text_re);
                if (!targetIndex.HasValue)
                {
                    throw new InvalidOperationException(
                        string.Format(
                            "未找到匹配 option: text={0}, value={1}, index={2}, text_contains={3}, text_re={4}",
                            FormatAny(text),
                            FormatAny(value),
                            index.HasValue ? index.Value.ToString() : "null",
                            FormatAny(text_contains),
                            FormatAny(text_re)));
                }

                SetDynamicProperty(_element, "selectedIndex", targetIndex.Value);
                for (var i = 0; i < options.Count; i++)
                    SetDynamicProperty(options[i], "selected", i == targetIndex.Value);

                var actualIndex = SafeInt(ReadDynamicProperty(_element, "selectedIndex"));
                if (!actualIndex.HasValue || actualIndex.Value != targetIndex.Value)
                {
                    throw new InvalidOperationException(
                        string.Format(
                            "select_option未生效: expected.selectedIndex={0}, actual.selectedIndex={1}, element={2}",
                            targetIndex.Value,
                            actualIndex.HasValue ? actualIndex.Value.ToString() : "null",
                            DescribeSelf()));
                }

                if (trigger_events)
                    TriggerSelectEvents(trigger_dblclick);

                if (delay_after > 0)
                    Thread.Sleep(ToSleepMilliseconds(delay_after));
            }

            public void check(
                bool trigger_events = true,
                int delay_before = 0,
                int delay_after = 0)
            {
                SetCheckedState(true, trigger_events, delay_before, delay_after);
            }

            public void uncheck(
                bool trigger_events = true,
                int delay_before = 0,
                int delay_after = 0)
            {
                SetCheckedState(false, trigger_events, delay_before, delay_after);
            }

            private void SetCheckedState(
                bool targetChecked,
                bool triggerEvents,
                int delayBefore,
                int delayAfter)
            {
                if (delayBefore > 0)
                    Thread.Sleep(ToSleepMilliseconds(delayBefore));

                SafeRead(() => InvokeDynamicMethod(_element, "focus"));

                var typeText = SafeToString(ReadDynamicProperty(_element, "type"));
                var tagText = SafeToString(ReadDynamicProperty(_element, "tagName"));
                var isCheckableInput = string.Equals(tagText, "input", StringComparison.OrdinalIgnoreCase)
                    && (string.Equals(typeText, "checkbox", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(typeText, "radio", StringComparison.OrdinalIgnoreCase));

                if (!isCheckableInput)
                    throw new InvalidOperationException("目标元素不是 checkbox/radio，无法执行 check/uncheck");

                var currentChecked = IsChecked();
                if (currentChecked == targetChecked)
                {
                    if (delayAfter > 0)
                        Thread.Sleep(ToSleepMilliseconds(delayAfter));
                    return;
                }

                SetDynamicProperty(_element, "checked", targetChecked);
                SafeRead(() => InvokeDynamicMethod(_element, "setAttribute", "checked", targetChecked ? "checked" : null));

                if (triggerEvents)
                    TriggerCheckEvents();

                var checkedAfter = IsChecked();
                if (checkedAfter != targetChecked)
                {
                    throw new InvalidOperationException(
                        string.Format(
                            "{0}未生效: expected.checked={1}, actual.checked={2}, element={3}",
                            targetChecked ? "check" : "uncheck",
                            targetChecked,
                            checkedAfter,
                            DescribeSelf()));
                }

                if (delayAfter > 0)
                    Thread.Sleep(ToSleepMilliseconds(delayAfter));
            }

            private bool IsChecked()
            {
                try
                {
                    var value = ReadDynamicProperty(_element, "checked");
                    if (value == null)
                        return false;
                    return Convert.ToBoolean(value);
                }
                catch
                {
                    return false;
                }
            }

            private void TriggerCheckEvents()
            {
                FireLegacyEvent("onclick");
                FireLegacyEvent("onchange");

                DispatchStandardEvent("click");
                DispatchStandardEvent("input");
                DispatchStandardEvent("change");
            }

            public string get_text()
            {
                var text = NormalizeText(SafeToString(ReadDynamicProperty(_element, "innerText")));
                if (!string.IsNullOrWhiteSpace(text))
                    return text;

                text = NormalizeText(SafeToString(ReadDynamicProperty(_element, "textContent")));
                if (!string.IsNullOrWhiteSpace(text))
                    return text;

                text = SafeToString(ReadDynamicProperty(_element, "value"));
                if (!string.IsNullOrWhiteSpace(text))
                    return text;

                return string.Empty;
            }

            public object get_attribute(string name, object defaultValue = null)
            {
                if (string.IsNullOrWhiteSpace(name))
                    return defaultValue;

                var value = SafeRead(() => InvokeDynamicMethod(_element, "getAttribute", name));
                if (value != null)
                    return value;

                value = ReadDynamicProperty(_element, name);
                return value ?? defaultValue;
            }

            public void set_attribute(
                string name,
                object value,
                bool trigger_events = true,
                int delay_before = 0,
                int delay_after = 0)
            {
                if (string.IsNullOrWhiteSpace(name))
                    throw new ArgumentNullException(nameof(name));

                DebugLog("set_attribute", "begin element={0}, name={1}, value={2}, trigger_events={3}", DescribeSelf(), FormatAny(name), FormatAny(value), trigger_events);

                if (delay_before > 0)
                    Thread.Sleep(ToSleepMilliseconds(delay_before));

                SafeRead(() => InvokeDynamicMethod(_element, "focus"));
                SetDynamicProperty(_element, name, value);
                SafeRead(() => InvokeDynamicMethod(_element, "setAttribute", name, value));

                var expected = SafeToString(value);
                var actualProperty = SafeToString(ReadDynamicProperty(_element, name));
                var actualAttribute = SafeToString(SafeRead(() => InvokeDynamicMethod(_element, "getAttribute", name)));
                if (!IsEquivalentValue(actualProperty, expected) && !IsEquivalentValue(actualAttribute, expected))
                {
                    DebugLog("set_attribute", "verify failed element={0}, actual.property={1}, actual.attribute={2}", DescribeSelf(), FormatAny(actualProperty), FormatAny(actualAttribute));
                    throw new InvalidOperationException(
                        string.Format(
                            "set_attribute 未生效: name={0}, expected={1}, actual.property={2}, actual.attribute={3}, element={4}",
                            FormatAny(name),
                            FormatAny(expected),
                            FormatAny(actualProperty),
                            FormatAny(actualAttribute),
                            DescribeSelf()));
                }

                DebugLog("set_attribute", "verify success element={0}, actual.property={1}, actual.attribute={2}", DescribeSelf(), FormatAny(actualProperty), FormatAny(actualAttribute));

                if (trigger_events)
                {
                    FireLegacyEvent("onchange");
                    DispatchStandardEvent("change");
                }

                if (delay_after > 0)
                    Thread.Sleep(ToSleepMilliseconds(delay_after));
            }

            private static bool IsEquivalentValue(string actual, string expected)
            {
                return string.Equals(
                    NormalizeText(actual),
                    NormalizeText(expected),
                    StringComparison.Ordinal);
            }

            private string DescribeSelf()
            {
                var tag = SafeToString(ReadDynamicProperty(_element, "tagName"));
                var type = SafeToString(ReadDynamicProperty(_element, "type"));
                var id = SafeToString(ReadDynamicProperty(_element, "id"));
                var name = SafeToString(ReadDynamicProperty(_element, "name"));
                return string.Format("tag={0}, type={1}, id={2}, name={3}", FormatAny(tag), FormatAny(type), FormatAny(id), FormatAny(name));
            }

            public void press_keys(
                object keys,
                int delay_before = 0,
                int delay_after = 0)
            {
                if (delay_before > 0)
                    Thread.Sleep(ToSleepMilliseconds(delay_before));

                SafeRead(() => InvokeDynamicMethod(_element, "focus"));

                var keyTokens = NormalizeKeyTokens(keys);
                if (keyTokens.Count == 0)
                {
                    if (delay_after > 0)
                        Thread.Sleep(ToSleepMilliseconds(delay_after));
                    return;
                }

                var ctrl = false;
                var alt = false;
                var shift = false;
                var meta = false;
                string mainKey = null;

                for (var i = 0; i < keyTokens.Count; i++)
                {
                    var token = keyTokens[i];
                    if (IsCtrlToken(token))
                    {
                        ctrl = true;
                        continue;
                    }

                    if (IsAltToken(token))
                    {
                        alt = true;
                        continue;
                    }

                    if (IsShiftToken(token))
                    {
                        shift = true;
                        continue;
                    }

                    if (IsMetaToken(token))
                    {
                        meta = true;
                        continue;
                    }

                    mainKey = token;
                }

                if (string.IsNullOrWhiteSpace(mainKey))
                    mainKey = keyTokens[keyTokens.Count - 1];

                var keyCode = ResolveKeyCode(mainKey);
                TriggerKeyboardEvents(keyCode, ctrl, alt, shift, meta);

                if (ShouldAppendTypedCharacter(mainKey, ctrl, alt, meta))
                {
                    var current = SafeToString(ReadDynamicProperty(_element, "value"));
                    SetDynamicProperty(_element, "value", current + mainKey);
                }

                if (delay_after > 0)
                    Thread.Sleep(ToSleepMilliseconds(delay_after));
            }

            public void double_click(
                int delay_before = 0,
                int delay_after = 0)
            {
                if (delay_before > 0)
                    Thread.Sleep(ToSleepMilliseconds(delay_before));

                SafeRead(() => InvokeDynamicMethod(_element, "focus"));
                SafeRead(() => InvokeDynamicMethod(_element, "click"));
                SafeRead(() => InvokeDynamicMethod(_element, "click"));

                SafeRead(() => InvokeDynamicMethod(_element, "fireEvent", "ondblclick"));
                DispatchStandardEvent("dblclick");

                if (delay_after > 0)
                    Thread.Sleep(ToSleepMilliseconds(delay_after));
            }

            public IEDomElement get_parent()
            {
                var parent = ReadDynamicProperty(_element, "parentElement")
                    ?? ReadDynamicProperty(_element, "parentNode");
                if (parent == null)
                    return null;

                return WrapElement(parent);
            }

            public IEDomElement[] get_children()
            {
                var children = ReadDynamicProperty(_element, "children")
                    ?? ReadDynamicProperty(_element, "childNodes");

                var result = new List<IEDomElement>();
                foreach (var child in EnumerateIndexedCollection(children))
                {
                    if (child == null)
                        continue;
                    result.Add(WrapElement(child));
                }

                return result.ToArray();
            }

            public bool is_checked()
            {
                var tagText = SafeToString(ReadDynamicProperty(_element, "tagName"));
                var typeText = SafeToString(ReadDynamicProperty(_element, "type"));

                if (string.Equals(tagText, "input", StringComparison.OrdinalIgnoreCase)
                    && (string.Equals(typeText, "checkbox", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(typeText, "radio", StringComparison.OrdinalIgnoreCase)))
                {
                    return IsChecked();
                }

                if (string.Equals(tagText, "select", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(typeText, "select-one", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(typeText, "select-multiple", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(typeText, "combobox", StringComparison.OrdinalIgnoreCase))
                {
                    var selectedIndex = SafeInt(ReadDynamicProperty(_element, "selectedIndex"));
                    return selectedIndex.HasValue && selectedIndex.Value >= 0;
                }

                throw new InvalidOperationException("is_checked 仅支持 radio/checkbox 与 combobox(select)");
            }

            public string[] get_selected_options()
            {
                var tagText = SafeToString(ReadDynamicProperty(_element, "tagName"));
                if (!string.Equals(tagText, "select", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("get_selected_options 仅支持 dropdown(select)");

                var optionsCollection = ReadDynamicProperty(_element, "options");
                var selected = new List<string>();

                foreach (var option in EnumerateIndexedCollection(optionsCollection))
                {
                    if (option == null)
                        continue;

                    var isSelected = false;
                    try
                    {
                        isSelected = Convert.ToBoolean(ReadDynamicProperty(option, "selected"));
                    }
                    catch
                    {
                        isSelected = false;
                    }

                    if (!isSelected)
                        continue;

                    var text = NormalizeText(SafeToString(ReadDynamicProperty(option, "text")));
                    if (string.IsNullOrWhiteSpace(text))
                        text = NormalizeText(SafeToString(ReadDynamicProperty(option, "innerText")));
                    if (string.IsNullOrWhiteSpace(text))
                        text = SafeToString(ReadDynamicProperty(option, "value"));

                    selected.Add(text);
                }

                return selected.ToArray();
            }

            private IEDomElement WrapElement(object element)
            {
                var ownerDocument = ReadDynamicProperty(element, "ownerDocument")
                    ?? ReadDynamicProperty(element, "document")
                    ?? _document;
                return new IEDomElement(_controller, element, ownerDocument);
            }

            private static List<string> NormalizeKeyTokens(object keys)
            {
                var tokens = new List<string>();
                if (keys == null)
                    return tokens;

                var keyString = keys as string;
                if (keyString != null)
                {
                    var parts = keyString.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
                    for (var i = 0; i < parts.Length; i++)
                    {
                        var token = parts[i].Trim();
                        if (!string.IsNullOrWhiteSpace(token))
                            tokens.Add(token);
                    }

                    if (tokens.Count == 0 && !string.IsNullOrWhiteSpace(keyString))
                        tokens.Add(keyString.Trim());

                    return tokens;
                }

                var enumerable = keys as IEnumerable;
                if (enumerable == null)
                {
                    tokens.Add(SafeToString(keys));
                    return tokens;
                }

                foreach (var item in enumerable)
                {
                    var token = SafeToString(item).Trim();
                    if (!string.IsNullOrWhiteSpace(token))
                        tokens.Add(token);
                }

                return tokens;
            }

            private static bool IsCtrlToken(string token)
            {
                return string.Equals(token, "CTRL", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(token, "CONTROL", StringComparison.OrdinalIgnoreCase);
            }

            private static bool IsAltToken(string token)
            {
                return string.Equals(token, "ALT", StringComparison.OrdinalIgnoreCase);
            }

            private static bool IsShiftToken(string token)
            {
                return string.Equals(token, "SHIFT", StringComparison.OrdinalIgnoreCase);
            }

            private static bool IsMetaToken(string token)
            {
                return string.Equals(token, "META", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(token, "WIN", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(token, "WINDOWS", StringComparison.OrdinalIgnoreCase);
            }

            private static int ResolveKeyCode(string key)
            {
                if (string.IsNullOrWhiteSpace(key))
                    return 0;

                var token = key.Trim().ToUpperInvariant();
                if (token.Length == 1)
                    return Convert.ToInt32(token[0]);

                if (token.StartsWith("F", StringComparison.Ordinal) && token.Length <= 3)
                {
                    if (int.TryParse(token.Substring(1), out var fn) && fn >= 1 && fn <= 12)
                        return 111 + fn;
                }

                switch (token)
                {
                    case "ENTER": return 13;
                    case "TAB": return 9;
                    case "ESC":
                    case "ESCAPE": return 27;
                    case "SPACE": return 32;
                    case "LEFT": return 37;
                    case "UP": return 38;
                    case "RIGHT": return 39;
                    case "DOWN": return 40;
                    case "DELETE":
                    case "DEL": return 46;
                    case "BACKSPACE": return 8;
                    case "HOME": return 36;
                    case "END": return 35;
                    case "PAGEUP": return 33;
                    case "PAGEDOWN": return 34;
                    case "INSERT": return 45;
                    default:
                        return 0;
                }
            }

            private static bool ShouldAppendTypedCharacter(string key, bool ctrl, bool alt, bool meta)
            {
                if (ctrl || alt || meta)
                    return false;
                if (string.IsNullOrWhiteSpace(key) || key.Length != 1)
                    return false;
                return true;
            }

            private void TriggerKeyboardEvents(int keyCode, bool ctrl, bool alt, bool shift, bool meta)
            {
                FireLegacyKeyboardEvent("onkeydown", keyCode, ctrl, alt, shift, meta);
                FireLegacyKeyboardEvent("onkeypress", keyCode, ctrl, alt, shift, meta);
                FireLegacyKeyboardEvent("onkeyup", keyCode, ctrl, alt, shift, meta);

                DispatchStandardEvent("keydown");
                DispatchStandardEvent("keypress");
                DispatchStandardEvent("keyup");
            }

            private void FireLegacyKeyboardEvent(string eventName, int keyCode, bool ctrl, bool alt, bool shift, bool meta)
            {
                SafeRead(() =>
                {
                    var evt = InvokeDynamicMethod(_document, "createEventObject");
                    if (evt != null)
                    {
                        SetDynamicProperty(evt, "keyCode", keyCode);
                        SetDynamicProperty(evt, "which", keyCode);
                        SetDynamicProperty(evt, "ctrlKey", ctrl);
                        SetDynamicProperty(evt, "altKey", alt);
                        SetDynamicProperty(evt, "shiftKey", shift);
                        SetDynamicProperty(evt, "metaKey", meta);
                    }

                    return InvokeDynamicMethod(_element, "fireEvent", eventName, evt);
                });
            }

            private int? ResolveOptionIndex(
                IList<object> options,
                string text,
                string value,
                int? index,
                string textContains,
                string textRegex)
            {
                if (options == null || options.Count == 0)
                    return null;

                if (index.HasValue)
                {
                    if (index.Value >= 0 && index.Value < options.Count)
                        return index.Value;
                    return null;
                }

                var hasText = !string.IsNullOrWhiteSpace(text);
                var hasValue = !string.IsNullOrWhiteSpace(value);
                var hasTextContains = !string.IsNullOrWhiteSpace(textContains);
                var hasTextRegex = !string.IsNullOrWhiteSpace(textRegex);

                Regex compiledRegex = null;
                if (hasTextRegex)
                {
                    try
                    {
                        compiledRegex = new Regex(textRegex, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("text_re 不是有效正则表达式: " + ex.Message, ex);
                    }
                }

                for (var i = 0; i < options.Count; i++)
                {
                    var option = options[i];
                    var optionText = NormalizeText(SafeToString(ReadDynamicProperty(option, "text")));
                    if (string.IsNullOrWhiteSpace(optionText))
                        optionText = NormalizeText(SafeToString(ReadDynamicProperty(option, "innerText")));
                    var optionValue = SafeToString(ReadDynamicProperty(option, "value"));

                    if (hasText && !string.Equals(optionText, text, StringComparison.Ordinal))
                        continue;
                    if (hasValue && !string.Equals(optionValue, value, StringComparison.Ordinal))
                        continue;
                    if (hasTextContains && optionText.IndexOf(textContains, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    if (compiledRegex != null && !compiledRegex.IsMatch(optionText))
                        continue;

                    return i;
                }

                if (!hasText && !hasValue && !hasTextContains && !hasTextRegex)
                    return 0;

                return null;
            }


            private void TriggerSelectEvents(bool triggerDblclick)
            {
                FireLegacyEvent("onchange");
                FireLegacyEvent("onclick");

                DispatchStandardEvent("input");
                DispatchStandardEvent("change");
                DispatchStandardEvent("click");

                if (triggerDblclick)
                {
                    FireLegacyEvent("ondblclick");
                    DispatchStandardEvent("dblclick");
                }
            }

            private void TriggerInputEvents()
            {
                var eventNames = new[] { "oninput", "onchange", "onblur" };
                for (var i = 0; i < eventNames.Length; i++)
                    FireLegacyEvent(eventNames[i]);

                DispatchStandardEvent("input");
                DispatchStandardEvent("change");
            }

            private void FireLegacyEvent(string eventName)
            {
                SafeRead(() =>
                {
                    var evt = InvokeDynamicMethod(_document, "createEventObject");
                    return InvokeDynamicMethod(_element, "fireEvent", eventName, evt);
                });
            }

            private void DispatchStandardEvent(string eventName)
            {
                SafeRead(() =>
                {
                    var evt = InvokeDynamicMethod(_document, "createEvent", "HTMLEvents");
                    if (evt != null)
                    {
                        SafeRead(() => InvokeDynamicMethod(evt, "initEvent", eventName, true, true));
                        return InvokeDynamicMethod(_element, "dispatchEvent", evt);
                    }

                    return null;
                });
            }
        }

        private static class NativeMethods
        {
            internal const uint SmtoAbortIfHung = 0x0002;

            internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

            [DllImport("user32.dll")]
            internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

            [DllImport("user32.dll")]
            internal static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            internal static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            internal static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

            [DllImport("user32.dll")]
            internal static extern bool IsWindow(IntPtr hWnd);

            [DllImport("user32.dll")]
            internal static extern bool IsWindowVisible(IntPtr hWnd);

            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            internal static extern uint RegisterWindowMessage(string lpString);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            internal static extern IntPtr SendMessageTimeout(
                IntPtr hWnd,
                uint msg,
                IntPtr wParam,
                IntPtr lParam,
                uint fuFlags,
                uint uTimeout,
                out IntPtr lpdwResult);

            [DllImport("oleacc.dll", PreserveSig = true)]
            internal static extern int ObjectFromLresult(
                IntPtr lResult,
                ref Guid riid,
                uint wParam,
                [MarshalAs(UnmanagedType.Interface)] out object ppvObject);
        }
    }
}
