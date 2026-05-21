using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using F2B.Browser.IExplore;

namespace F2B.Browser.IExplore.Com
{
    /// <summary>MSHTML element location aligned with Python <c>_locate_elements</c> / <c>_matches_locator</c>.</summary>
    internal static class HtmlElementFinder
    {
        internal const int PollIntervalMs = 200;

        public static object Find(
            IHTMLDocument2 document,
            ParsedElementLocator parsed,
            int timeoutMs = OperationDefaults.TimeoutMs)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            if (parsed == null)
                throw new ArgumentNullException(nameof(parsed));

            OperationTimeout.Validate(timeoutMs, nameof(timeoutMs));
            var filterDesc = DescribeLocator(parsed);

            return OperationTimeout.WaitUntil(
                timeoutMs,
                PollIntervalMs,
                () => TryFind(document, parsed),
                () => new TimeoutException(
                    "Timed out after " + timeoutMs + " ms waiting for element: " + filterDesc));
        }

        /// <summary>Instant snapshot of all current matches (no wait). Empty array if none.</summary>
        public static object[] FindAll(IHTMLDocument2 document, ParsedElementLocator parsed) =>
            TryFindAll(document, parsed);

        /// <summary>Instant snapshot under <paramref name="scopeElement"/> (no wait). Empty array if none.</summary>
        public static object[] FindAllInScope(object scopeElement, ParsedElementLocator parsed) =>
            TryFindAllInScope(scopeElement, parsed);

        public static object FindInScope(
            object scopeElement,
            ParsedElementLocator parsed,
            int timeoutMs = OperationDefaults.TimeoutMs)
        {
            if (!ComElementHelper.IsValidElement(scopeElement))
                throw new ArgumentNullException(nameof(scopeElement));
            if (parsed == null)
                throw new ArgumentNullException(nameof(parsed));

            OperationTimeout.Validate(timeoutMs, nameof(timeoutMs));
            var filterDesc = DescribeLocator(parsed);

            return OperationTimeout.WaitUntil(
                timeoutMs,
                PollIntervalMs,
                () => TryFindInScope(scopeElement, parsed),
                () => new TimeoutException(
                    "Timed out after " + timeoutMs + " ms waiting for element in scope: " + filterDesc));
        }

        internal static object TryFindOnce(
            IHTMLDocument2 document,
            ParsedElementLocator parsed)
        {
            if (document == null || parsed == null)
                return null;

            return PickMatch(LocateElements(document, parsed), parsed.ElementIdx);
        }

        internal static object[] TryFindAll(IHTMLDocument2 document, ParsedElementLocator parsed)
        {
            if (document == null || parsed == null)
                return new object[0];

            var matches = LocateElements(document, parsed);
            return matches == null || matches.Count == 0 ? new object[0] : matches.ToArray();
        }

        internal static object TryFindOnceInScope(
            object scopeElement,
            ParsedElementLocator parsed)
        {
            if (!ComElementHelper.IsValidElement(scopeElement) || parsed == null)
                return null;

            return PickMatch(LocateElementsInScope(scopeElement, parsed), parsed.ElementIdx);
        }

        private static object TryFind(IHTMLDocument2 document, ParsedElementLocator parsed) =>
            PickMatch(LocateElements(document, parsed), parsed.ElementIdx);

        private static object TryFindInScope(object scopeElement, ParsedElementLocator parsed) =>
            PickMatch(LocateElementsInScope(scopeElement, parsed), parsed.ElementIdx);

        internal static object[] TryFindAllInScope(object scopeElement, ParsedElementLocator parsed)
        {
            if (!ComElementHelper.IsValidElement(scopeElement) || parsed == null)
                return new object[0];

            var matches = LocateElementsInScope(scopeElement, parsed);
            return matches == null || matches.Count == 0 ? new object[0] : matches.ToArray();
        }

        /// <summary>Python <c>_locate_elements</c>.</summary>
        private static List<object> LocateElements(IHTMLDocument2 document, ParsedElementLocator parsed)
        {
            string id;
            if (parsed.Filters.TryGetValue(ElementLocatorKeys.Id, out id) && !string.IsNullOrEmpty(id))
            {
                try
                {
                    dynamic doc = document;
                    var candidate = doc.getElementById(id);
                    if (ComElementHelper.IsValidElement(candidate))
                    {
                        if (MatchesLocator(candidate, parsed))
                            return new List<object> { candidate };

                        Console.WriteLine("C#getElementById('" + id + "') 命中但过滤条件不符");
                    }
                }
                catch
                {
                    // fall through
                }

                return new List<object>();
            }

            if (!string.IsNullOrWhiteSpace(parsed.CssSelector))
            {
                return FilterMatches(QueryCss(document, parsed.CssSelector), parsed);
            }

            if (!string.IsNullOrWhiteSpace(parsed.XPath))
            {
                return FilterMatches(QueryXPath(document, parsed.XPath), parsed);
            }

            return FilterMatches(IterateDocumentElements(document, GetTagFilter(parsed)), parsed);
        }

        private static List<object> LocateElementsInScope(object scopeElement, ParsedElementLocator parsed)
        {
            return FilterMatches(IterateCollection(scopeElement, GetTagFilter(parsed)), parsed);
        }

        private static List<object> FilterMatches(IEnumerable<object> candidates, ParsedElementLocator parsed)
        {
            var matches = new List<object>();
            if (candidates == null)
                return matches;

            foreach (var el in candidates)
            {
                if (ComElementHelper.IsValidElement(el) && MatchesLocator(el, parsed))
                    matches.Add(el);
            }

            return matches;
        }

        private static string GetTagFilter(ParsedElementLocator parsed)
        {
            string tag;
            if (parsed.Filters.TryGetValue(ElementLocatorKeys.Tag, out tag) && !string.IsNullOrWhiteSpace(tag))
                return tag.Trim();
            return "*";
        }

        private static IEnumerable<object> QueryCss(IHTMLDocument2 document, string selector)
        {
            if (string.IsNullOrWhiteSpace(selector))
                yield break;

            dynamic doc = document;
            dynamic query;
            try
            {
                query = doc.querySelectorAll;
            }
            catch
            {
                yield break;
            }

            if (query == null)
                yield break;

            dynamic results;
            try
            {
                results = query(selector);
            }
            catch
            {
                yield break;
            }

            if (results == null)
                yield break;

            int length;
            try { length = (int)results.length; }
            catch { yield break; }

            for (int i = 0; i < length; i++)
            {
                object el;
                try { el = results.item(i); }
                catch { continue; }

                if (ComElementHelper.IsValidElement(el))
                    yield return el;
            }
        }

        private static IEnumerable<object> QueryXPath(IHTMLDocument2 document, string xpath)
        {
            if (string.IsNullOrWhiteSpace(xpath))
                yield break;

            dynamic doc = document;
            object docElement = null;
            try { docElement = doc.documentElement; }
            catch { /* ignore */ }

            object found = null;
            foreach (var owner in new[] { doc, docElement })
            {
                if (owner == null)
                    continue;

                try
                {
                    dynamic dyn = owner;
                    var node = dyn.selectSingleNode(xpath);
                    if (ComElementHelper.IsValidElement(node))
                    {
                        found = node;
                        break;
                    }
                }
                catch
                {
                    // try next owner
                }
            }

            if (found != null)
                yield return found;
        }

        private static IEnumerable<object> IterateDocumentElements(IHTMLDocument2 document, string tag) =>
            IterateCollection(document, tag);

        /// <summary>Python <c>_iter_document_elements</c>: tag via getElementsByTagName, fallback to <c>all</c>.</summary>
        private static IEnumerable<object> IterateCollection(dynamic root, string tag)
        {
            if (root == null)
                yield break;

            tag = string.IsNullOrWhiteSpace(tag) ? "*" : tag.Trim();
            dynamic collection = null;

            try
            {
                collection = root.getElementsByTagName(tag);
            }
            catch
            {
                collection = null;
            }

            if (collection == null)
            {
                try { collection = root.all; }
                catch { yield break; }
            }

            if (collection == null)
                yield break;

            int length;
            try { length = (int)collection.length; }
            catch { yield break; }

            for (int i = 0; i < length; i++)
            {
                object el;
                try { el = collection.item(i); }
                catch { continue; }

                if (ComElementHelper.IsValidElement(el))
                    yield return el;
            }
        }

        /// <summary>Python <c>_matches_locator</c>.</summary>
        private static bool MatchesLocator(object element, ParsedElementLocator parsed)
        {
            string tag;
            if (parsed.Filters.TryGetValue(ElementLocatorKeys.Tag, out tag) && !string.IsNullOrEmpty(tag))
            {
                if (!StringEquals(GetStringProp(element, "tagName"), tag))
                    return false;
            }

            string name;
            if (parsed.Filters.TryGetValue(ElementLocatorKeys.Name, out name))
            {
                if (!StringEquals(GetStringProp(element, "name"), name))
                    return false;
            }

            if (parsed.Filters.ContainsKey(ElementLocatorKeys.Id))
            {
                string id;
                parsed.Filters.TryGetValue(ElementLocatorKeys.Id, out id);
                if (!StringEquals(GetStringProp(element, "id"), id))
                    return false;
            }

            if (parsed.Text != null)
            {
                if (ElementText(element).Trim() != parsed.Text.Trim())
                    return false;
            }

            if (parsed.TextContains != null)
            {
                if (ElementText(element).IndexOf(parsed.TextContains, StringComparison.Ordinal) < 0)
                    return false;
            }

            if (!string.IsNullOrEmpty(parsed.TextRe))
            {
                if (!Regex.IsMatch(ElementText(element), parsed.TextRe))
                    return false;
            }

            foreach (var kv in parsed.Filters)
            {
                if (IsReservedFilterKey(kv.Key))
                    continue;

                if (kv.Key.Equals(ElementLocatorKeys.Class, StringComparison.OrdinalIgnoreCase))
                {
                    if (!ClassMatches(GetStringProp(element, "className"), kv.Value))
                        return false;
                    continue;
                }

                var attr = GetAttribute(element, kv.Key);
                if (!StringEquals(attr, kv.Value))
                    return false;
            }

            if (parsed.Attrs != null)
            {
                foreach (var kv in parsed.Attrs)
                {
                    var actual = GetAttribute(element, kv.Key);
                    if (!StringEquals(actual, kv.Value))
                        return false;
                }
            }

            return true;
        }

        private static bool IsReservedFilterKey(string key) =>
            key.Equals(ElementLocatorKeys.Id, StringComparison.OrdinalIgnoreCase)
            || key.Equals(ElementLocatorKeys.Tag, StringComparison.OrdinalIgnoreCase)
            || key.Equals(ElementLocatorKeys.Name, StringComparison.OrdinalIgnoreCase)
            || key.Equals(ElementLocatorKeys.Class, StringComparison.OrdinalIgnoreCase);

        private static string ElementText(object element)
        {
            foreach (var prop in new[] { "value", "innerText", "textContent", "outerText", "innerHTML" })
            {
                try
                {
                    dynamic el = element;
                    switch (prop)
                    {
                        case "value":
                            return (string)el.value ?? string.Empty;
                        case "innerText":
                            return (string)el.innerText ?? string.Empty;
                        case "textContent":
                            return (string)el.textContent ?? string.Empty;
                        case "outerText":
                            return (string)el.outerText ?? string.Empty;
                        case "innerHTML":
                            return (string)el.innerHTML ?? string.Empty;
                    }
                }
                catch
                {
                    // try next
                }
            }

            return string.Empty;
        }

        private static object[] ToArrayOrNull(List<object> matches) =>
            matches == null || matches.Count == 0 ? null : matches.ToArray();

        private static object PickMatch(List<object> matches, int idx)
        {
            if (matches == null || matches.Count <= idx)
                return null;

            return matches[idx];
        }

        private static bool ClassMatches(string className, string expected)
        {
            if (string.IsNullOrEmpty(expected))
                return string.IsNullOrEmpty(className);

            if (string.IsNullOrEmpty(className))
                return false;

            var tokens = className.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                if (token.Equals(expected, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return className.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetStringProp(object element, string prop)
        {
            try
            {
                dynamic el = element;
                if (prop == "id") return (string)el.id ?? string.Empty;
                if (prop == "tagName") return (string)el.tagName ?? string.Empty;
                if (prop == "name") return (string)el.name ?? string.Empty;
                if (prop == "className") return (string)el.className ?? string.Empty;
            }
            catch
            {
                // ignore
            }

            return string.Empty;
        }

        private static string GetAttribute(object element, string name)
        {
            try
            {
                dynamic el = element;
                var v = el.getAttribute(name);
                return v == null ? string.Empty : v.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool StringEquals(string a, string b) =>
            string.Equals(a ?? string.Empty, b ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        private static string DescribeLocator(ParsedElementLocator parsed)
        {
            var sb = new StringBuilder();
            foreach (var kv in parsed.Filters)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(kv.Key).Append('=').Append(kv.Value);
            }

            if (!string.IsNullOrEmpty(parsed.CssSelector))
                sb.Append(", css_selector=").Append(parsed.CssSelector);
            if (!string.IsNullOrEmpty(parsed.XPath))
                sb.Append(", xpath=").Append(parsed.XPath);
            if (parsed.Text != null)
                sb.Append(", text=").Append(parsed.Text);
            if (parsed.TextContains != null)
                sb.Append(", text_contains=").Append(parsed.TextContains);
            if (!string.IsNullOrEmpty(parsed.TextRe))
                sb.Append(", text_re=").Append(parsed.TextRe);

            return sb.ToString();
        }
    }
}
