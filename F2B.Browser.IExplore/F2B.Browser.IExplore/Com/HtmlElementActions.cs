using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using F2B.Browser.IExplore;
using F2B.Browser.IExplore.Native;

namespace F2B.Browser.IExplore.Com
{
    internal static class HtmlElementActions
    {
        public static IEHtmlElement FindElement(
            ITridentDomHost window,
            IDictionary<string, object> element,
            IList<IDictionary<string, object>> framePath,
            IEHtmlElement scope,
            int timeout)
        {
            var parsed = ElementLocatorParse.Parse(element, LocatorOperation.Element);
            var raw = FindRawElement(window, parsed, framePath, scope, timeout);
            return IEHtmlElement.From(raw);
        }

        public static IEHtmlElement[] FindElements(
            ITridentDomHost window,
            IDictionary<string, object> element,
            IList<IDictionary<string, object>> framePath,
            IEHtmlElement scope,
            int timeout)
        {
            var parsed = ElementLocatorParse.Parse(element, LocatorOperation.Element);
            var raws = FindRawElements(window, parsed.Filters, framePath, scope, timeout);
            var result = new IEHtmlElement[raws.Length];
            for (int i = 0; i < raws.Length; i++)
                result[i] = IEHtmlElement.From(raws[i]);
            return result;
        }

        /// <summary>Instant check whether an element matches; does not wait or throw.</summary>
        public static bool ElementExists(
            ITridentDomHost window,
            IDictionary<string, object> element,
            IList<IDictionary<string, object>> framePath,
            IEHtmlElement scope)
        {
            if (window == null)
                throw new ArgumentNullException(nameof(window));

            var parsed = ElementLocatorParse.Parse(element, LocatorOperation.Element);
            object raw;

            if (scope != null)
            {
                if (framePath != null && framePath.Count > 0)
                    throw new ArgumentException("framePath cannot be used with a parent element.", nameof(framePath));

                raw = HtmlElementFinder.TryFindOnceInScope(
                    IEHtmlElement.Unwrap(scope),
                    parsed.Filters,
                    parsed.ElementIdx);
            }
            else
            {
                raw = TryFindRawElementOnce(window, parsed, framePath);
            }

            return raw != null && ComElementHelper.IsValidElement(raw);
        }

        /// <summary>
        /// Poll until any locator matches. When several match in the same poll cycle, the lowest index wins.
        /// Throws if none match within <paramref name="timeout"/>.
        /// </summary>
        public static ParallelFindElementResult ParallelFindElement(
            ITridentDomHost window,
            IELocator[] locators,
            int timeout)
        {
            if (window == null)
                throw new ArgumentNullException(nameof(window));
            if (locators == null)
                throw new ArgumentNullException(nameof(locators));
            if (locators.Length == 0)
                throw new ArgumentException("At least one locator is required.", nameof(locators));

            for (int i = 0; i < locators.Length; i++)
            {
                if (locators[i] == null)
                    throw new ArgumentException("Locator at index " + i + " is null.", nameof(locators));
            }

            OperationTimeout.Validate(timeout, nameof(timeout));

            var parsedLocators = new ParsedElementLocator[locators.Length];
            var framePaths = new IList<IDictionary<string, object>>[locators.Length];
            for (int i = 0; i < locators.Length; i++)
            {
                parsedLocators[i] = ElementLocatorParse.Parse(locators[i].ParseElement(), LocatorOperation.Element);
                framePaths[i] = locators[i].ParseFramePath();
            }

            return OperationTimeout.WaitUntil(
                timeout,
                () => TryParallelFindOnce(window, parsedLocators, framePaths),
                () => new TimeoutException(
                    "Timed out after " + timeout + " ms waiting for any of " + locators.Length + " locators."));
        }

        private static ParallelFindElementResult TryParallelFindOnce(
            ITridentDomHost window,
            ParsedElementLocator[] parsedLocators,
            IList<IDictionary<string, object>>[] framePaths)
        {
            for (int i = 0; i < parsedLocators.Length; i++)
            {
                object raw = null;
                try
                {
                    raw = TryFindRawElementOnce(window, parsedLocators[i], framePaths[i]);
                }
                catch
                {
                    // DOM/frame not ready yet
                }

                if (raw != null && ComElementHelper.IsValidElement(raw))
                    return new ParallelFindElementResult(i, IEHtmlElement.From(raw));
            }

            return null;
        }

        private static object TryFindRawElementOnce(
            ITridentDomHost window,
            ParsedElementLocator parsed,
            IList<IDictionary<string, object>> framePath)
        {
            IHTMLDocument2 doc;
            if (framePath == null || framePath.Count == 0)
            {
                doc = window.GetMsHtmlDocument();
            }
            else
            {
                var segments = FramePathParse.Parse(framePath);
                doc = HtmlFrameHelper.TryGetFrameDocument(window.GetMsHtmlDocument(), segments);
                if (doc == null)
                    return null;
            }

            return HtmlElementFinder.TryFindOnce(doc, parsed.Filters, parsed.ElementIdx);
        }

        public static void Click(
            ITridentDomHost window,
            IDictionary<string, object> element,
            IList<IDictionary<string, object>> framePath,
            int timeout)
        {
            var options = ElementLocatorOptions.Parse(element, forInput: false);
            var raw = FindRawElement(window, options, framePath, null, timeout);
            Click(window, IEHtmlElement.From(raw), options.Button, options.Mode, timeout);
        }

        public static void Click(
            ITridentDomHost window,
            IEHtmlElement element,
            int timeout = OperationDefaults.TimeoutMs) =>
            Click(window, element, MouseButton.Left, ClickMode.Synthetic, timeout);

        public static void Click(
            ITridentDomHost window,
            IEHtmlElement element,
            MouseButton button,
            ClickMode mode,
            int timeout = OperationDefaults.TimeoutMs)
        {
            var raw = IEHtmlElement.Unwrap(element);
            var doc = GetOwnerDocument(raw);
            if (mode == ClickMode.Physical || button != MouseButton.Left)
                PhysicalClick(window, doc, raw, button);
            else
                SyntheticClick(doc, raw);
        }

        public static void DoubleClick(
            ITridentDomHost window,
            IDictionary<string, object> element,
            IList<IDictionary<string, object>> framePath,
            int timeout)
        {
            var options = ElementLocatorOptions.Parse(element, forInput: false);
            var raw = FindRawElement(window, options, framePath, null, timeout);
            DoubleClick(window, IEHtmlElement.From(raw), options.Button, options.Mode, options.ClickIntervalMs, timeout);
        }

        public static void DoubleClick(
            ITridentDomHost window,
            IEHtmlElement element,
            int timeout = OperationDefaults.TimeoutMs) =>
            DoubleClick(window, element, MouseButton.Left, ClickMode.Synthetic, 100, timeout);

        public static void DoubleClick(
            ITridentDomHost window,
            IEHtmlElement element,
            MouseButton button,
            ClickMode mode,
            int clickIntervalMs,
            int timeout = OperationDefaults.TimeoutMs)
        {
            var raw = IEHtmlElement.Unwrap(element);
            var doc = GetOwnerDocument(raw);
            if (mode == ClickMode.Physical || button != MouseButton.Left)
                PhysicalDoubleClick(window, doc, raw, button, clickIntervalMs);
            else
                SyntheticDoubleClick(doc, raw);
        }

        public static void Input(
            ITridentDomHost window,
            IDictionary<string, object> element,
            IList<IDictionary<string, object>> framePath,
            int timeout)
        {
            var options = ElementLocatorOptions.Parse(element, forInput: true);
            var raw = FindRawElement(window, options, framePath, null, timeout);
            SetValue(raw, options.Value);
        }

        public static void Input(
            ITridentDomHost window,
            IEHtmlElement element,
            string value,
            int timeout = OperationDefaults.TimeoutMs) =>
            SetValue(IEHtmlElement.Unwrap(element), value);

        public static void Check(
            ITridentDomHost window,
            IDictionary<string, object> element,
            IList<IDictionary<string, object>> framePath,
            int timeout) =>
            HtmlElementDomHelper.SetChecked(
                FindRawElement(window, ElementLocatorParse.Parse(element, LocatorOperation.Element), framePath, null, timeout),
                true);

        public static void Check(ITridentDomHost window, IEHtmlElement element, int timeout) =>
            HtmlElementDomHelper.SetChecked(IEHtmlElement.Unwrap(element), true);

        public static void Uncheck(
            ITridentDomHost window,
            IDictionary<string, object> element,
            IList<IDictionary<string, object>> framePath,
            int timeout) =>
            HtmlElementDomHelper.SetChecked(
                FindRawElement(window, ElementLocatorParse.Parse(element, LocatorOperation.Element), framePath, null, timeout),
                false);

        public static void Uncheck(ITridentDomHost window, IEHtmlElement element, int timeout) =>
            HtmlElementDomHelper.SetChecked(IEHtmlElement.Unwrap(element), false);

        public static bool IsChecked(
            ITridentDomHost window,
            IDictionary<string, object> element,
            IList<IDictionary<string, object>> framePath,
            int timeout) =>
            HtmlElementDomHelper.IsChecked(
                FindRawElement(window, ElementLocatorParse.Parse(element, LocatorOperation.Element), framePath, null, timeout));

        public static bool IsChecked(ITridentDomHost window, IEHtmlElement element, int timeout) =>
            HtmlElementDomHelper.IsChecked(IEHtmlElement.Unwrap(element));

        public static void Select(
            ITridentDomHost window,
            IDictionary<string, object> element,
            IList<IDictionary<string, object>> framePath,
            int timeout)
        {
            var parsed = ElementLocatorParse.Parse(element, LocatorOperation.Select);
            var raw = FindRawElement(window, parsed, framePath, null, timeout);
            HtmlElementDomHelper.SelectOptions(raw, parsed.SelectCriteria);
        }

        public static string GetText(
            ITridentDomHost window,
            IDictionary<string, object> element,
            IList<IDictionary<string, object>> framePath,
            int timeout) =>
            HtmlElementDomHelper.GetText(
                FindRawElement(window, ElementLocatorParse.Parse(element, LocatorOperation.Element), framePath, null, timeout));

        public static string GetText(ITridentDomHost window, IEHtmlElement element, int timeout) =>
            HtmlElementDomHelper.GetText(IEHtmlElement.Unwrap(element));

        public static string GetValue(
            ITridentDomHost window,
            IDictionary<string, object> element,
            IList<IDictionary<string, object>> framePath,
            int timeout) =>
            HtmlElementDomHelper.GetValue(
                FindRawElement(window, ElementLocatorParse.Parse(element, LocatorOperation.Element), framePath, null, timeout));

        public static string GetValue(ITridentDomHost window, IEHtmlElement element, int timeout) =>
            HtmlElementDomHelper.GetValue(IEHtmlElement.Unwrap(element));

        public static string GetAttribute(
            ITridentDomHost window,
            IDictionary<string, object> element,
            string attributeName,
            IList<IDictionary<string, object>> framePath,
            int timeout) =>
            HtmlElementDomHelper.GetAttribute(
                FindRawElement(window, ElementLocatorParse.Parse(element, LocatorOperation.Element), framePath, null, timeout),
                attributeName);

        public static string GetAttribute(ITridentDomHost window, IEHtmlElement element, string attributeName, int timeout) =>
            HtmlElementDomHelper.GetAttribute(IEHtmlElement.Unwrap(element), attributeName);

        public static IHTMLDocument2 ResolveDocument(
            ITridentDomHost window,
            IList<IDictionary<string, object>> framePath,
            int timeout)
        {
            var root = window.GetMsHtmlDocument();
            if (framePath == null || framePath.Count == 0)
                return root;

            return HtmlFrameHelper.WaitForFrameDocument(root, framePath, timeout);
        }

        private static object FindRawElement(
            ITridentDomHost window,
            ElementLocatorOptions options,
            IList<IDictionary<string, object>> framePath,
            IEHtmlElement scope,
            int timeout) =>
            FindRawElement(window, options.Filters, options.Idx, framePath, scope, timeout);

        private static object FindRawElement(
            ITridentDomHost window,
            ParsedElementLocator parsed,
            IList<IDictionary<string, object>> framePath,
            IEHtmlElement scope,
            int timeout) =>
            FindRawElement(window, parsed.Filters, parsed.ElementIdx, framePath, scope, timeout);

        private static object FindRawElement(
            ITridentDomHost window,
            IDictionary<string, string> filters,
            int idx,
            IList<IDictionary<string, object>> framePath,
            IEHtmlElement scope,
            int timeout)
        {
            if (scope != null)
            {
                if (framePath != null && framePath.Count > 0)
                    throw new ArgumentException("framePath cannot be used with a scope element.", nameof(framePath));

                return HtmlElementFinder.FindInScope(IEHtmlElement.Unwrap(scope), filters, idx, timeout);
            }

            var doc = ResolveDocument(window, framePath, timeout);
            return HtmlElementFinder.Find(doc, filters, idx, timeout);
        }

        private static object[] FindRawElements(
            ITridentDomHost window,
            IDictionary<string, string> filters,
            IList<IDictionary<string, object>> framePath,
            IEHtmlElement scope,
            int timeout)
        {
            if (scope != null)
            {
                if (framePath != null && framePath.Count > 0)
                    throw new ArgumentException("framePath cannot be used with a scope element.", nameof(framePath));

                return HtmlElementFinder.FindAllInScope(IEHtmlElement.Unwrap(scope), filters, timeout);
            }

            var doc = ResolveDocument(window, framePath, timeout);
            return HtmlElementFinder.FindAll(doc, filters, timeout);
        }

        private static IHTMLDocument2 GetOwnerDocument(object element)
        {
            try
            {
                dynamic el = element;
                var doc = el.document as IHTMLDocument2;
                if (doc != null)
                    return doc;
            }
            catch { /* ignore */ }

            throw new InvalidOperationException("Could not resolve owner document for element.");
        }

        private static void SyntheticClick(IHTMLDocument2 doc, object element)
        {
            if (!ComElementHelper.IsValidElement(element))
                throw new InvalidOperationException("Element was not found (MSHTML returned null/DBNull).");

            PrepareElementForClick(element);
            IeScriptHelper.InvokeElementClick(doc, element, null, null);
        }

        private static void PhysicalClick(
            ITridentDomHost window,
            IHTMLDocument2 doc,
            object element,
            MouseButton button)
        {
            if (!TryGetElementCenterClient(element, out var x, out var y))
                throw new InvalidOperationException("Could not determine element coordinates for physical click.");

            PrepareElementForClick(element);
            System.Threading.Thread.Sleep(80);

            MouseInputHelper.ClickAtClientPoint(window.Handle, window.IeServerHandle, x, y, button);
            System.Threading.Thread.Sleep(50);

            IeScriptHelper.InvokeElementClick(doc, element, x, y);
        }

        private static void SyntheticDoubleClick(IHTMLDocument2 doc, object element)
        {
            if (!ComElementHelper.IsValidElement(element))
                throw new InvalidOperationException("Element was not found (MSHTML returned null/DBNull).");

            PrepareElementForClick(element);
            IeScriptHelper.InvokeElementDoubleClick(doc, element, null, null);
        }

        private static void PhysicalDoubleClick(
            ITridentDomHost window,
            IHTMLDocument2 doc,
            object element,
            MouseButton button,
            int clickIntervalMs)
        {
            if (!TryGetElementCenterClient(element, out var x, out var y))
                throw new InvalidOperationException("Could not determine element coordinates for physical double-click.");

            PrepareElementForClick(element);
            System.Threading.Thread.Sleep(80);

            MouseInputHelper.DoubleClickAtClientPoint(
                window.Handle,
                window.IeServerHandle,
                x,
                y,
                button,
                clickIntervalMs);
            System.Threading.Thread.Sleep(50);

            IeScriptHelper.InvokeElementDoubleClick(doc, element, x, y);
        }

        private static void PrepareElementForClick(object element)
        {
            try
            {
                dynamic el = element;
                el.scrollIntoView(true);
                el.focus();
            }
            catch { /* optional */ }

            System.Threading.Thread.Sleep(50);
        }

        private static bool TryGetElementCenterClient(object element, out int x, out int y)
        {
            x = 0;
            y = 0;
            if (TryGetCenterFromBoundingRect(element, out x, out y))
                return true;
            return TryGetCenterFromOffset(element, out x, out y);
        }

        private static bool TryGetCenterFromBoundingRect(object element, out int x, out int y)
        {
            x = 0;
            y = 0;
            try
            {
                dynamic el = element;
                dynamic rect = el.getBoundingClientRect();
                double width = rect.width;
                double height = rect.height;
                if (width <= 0 && height <= 0)
                    return false;

                x = (int)(rect.left + width / 2.0);
                y = (int)(rect.top + height / 2.0);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetCenterFromOffset(object element, out int x, out int y)
        {
            x = 0;
            y = 0;
            try
            {
                dynamic el = element;
                int left = el.offsetLeft;
                int top = el.offsetTop;
                int w = el.offsetWidth;
                int h = el.offsetHeight;

                dynamic parent = el.offsetParent;
                while (parent != null)
                {
                    try
                    {
                        left += parent.offsetLeft;
                        top += parent.offsetTop;
                        parent = parent.offsetParent;
                    }
                    catch
                    {
                        break;
                    }
                }

                x = left + Math.Max(w / 2, 1);
                y = top + Math.Max(h / 2, 1);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void SetValue(object element, string value)
        {
            if (!ComElementHelper.IsValidElement(element))
                throw new InvalidOperationException("Element was not found (MSHTML returned null/DBNull).");

            value = value ?? string.Empty;
            try
            {
                dynamic el = element;
                el.focus();
                el.value = value;
                try { el.fireEvent("onchange"); } catch { /* ignore */ }
                try { el.fireEvent("oninput"); } catch { /* ignore */ }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Input failed: " + ex.Message, ex);
            }
        }
    }
}
