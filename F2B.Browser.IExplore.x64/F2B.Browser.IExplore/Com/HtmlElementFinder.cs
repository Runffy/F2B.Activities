using System;
using System.Collections.Generic;
using System.Text;
using F2B.Browser.IExplore;

namespace F2B.Browser.IExplore.Com
{
    internal static class HtmlElementFinder
    {
        public static object Find(
            IHTMLDocument2 document,
            IDictionary<string, string> filters,
            int idx,
            int timeoutMs = OperationDefaults.TimeoutMs)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            ValidateFilters(filters, idx);

            OperationTimeout.Validate(timeoutMs, nameof(timeoutMs));
            var filterDesc = DescribeFilters(filters);

            return OperationTimeout.WaitUntil(
                timeoutMs,
                () => TryFind(document, filters, idx),
                () => new TimeoutException(
                    $"Timed out after {timeoutMs} ms waiting for element: {filterDesc}"));
        }

        /// <summary>
        /// Find all elements matching <paramref name="filters"/> (ignores index).
        /// Waits until at least one match exists, then returns every current match.
        /// </summary>
        public static object[] FindAll(
            IHTMLDocument2 document,
            IDictionary<string, string> filters,
            int timeoutMs = OperationDefaults.TimeoutMs)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            ValidateFilters(filters, idx: 0, requireIdx: false);

            OperationTimeout.Validate(timeoutMs, nameof(timeoutMs));
            var filterDesc = DescribeFilters(filters);

            return OperationTimeout.WaitUntil(
                timeoutMs,
                () => TryFindAll(document, filters),
                () => new TimeoutException(
                    $"Timed out after {timeoutMs} ms waiting for elements: {filterDesc}"));
        }

        /// <summary>Find all matching descendants inside <paramref name="scopeElement"/>.</summary>
        public static object[] FindAllInScope(
            object scopeElement,
            IDictionary<string, string> filters,
            int timeoutMs = OperationDefaults.TimeoutMs)
        {
            if (!ComElementHelper.IsValidElement(scopeElement))
                throw new ArgumentNullException(nameof(scopeElement));
            ValidateFilters(filters, idx: 0, requireIdx: false);

            OperationTimeout.Validate(timeoutMs, nameof(timeoutMs));
            var filterDesc = DescribeFilters(filters);

            return OperationTimeout.WaitUntil(
                timeoutMs,
                () => TryFindAllInScope(scopeElement, filters),
                () => new TimeoutException(
                    $"Timed out after {timeoutMs} ms waiting for elements in scope: {filterDesc}"));
        }

        /// <summary>Find a descendant inside <paramref name="scopeElement"/> (not the whole document).</summary>
        public static object FindInScope(
            object scopeElement,
            IDictionary<string, string> filters,
            int idx,
            int timeoutMs = OperationDefaults.TimeoutMs)
        {
            if (!ComElementHelper.IsValidElement(scopeElement))
                throw new ArgumentNullException(nameof(scopeElement));
            ValidateFilters(filters, idx);

            OperationTimeout.Validate(timeoutMs, nameof(timeoutMs));
            var filterDesc = DescribeFilters(filters);

            return OperationTimeout.WaitUntil(
                timeoutMs,
                () => TryFindInScope(scopeElement, filters, idx),
                () => new TimeoutException(
                    $"Timed out after {timeoutMs} ms waiting for element in scope: {filterDesc}"));
        }

        private static void ValidateFilters(IDictionary<string, string> filters, int idx, bool requireIdx = true)
        {
            if (filters == null || filters.Count == 0)
                throw new ArgumentException("No element filters provided.", nameof(filters));
            if (requireIdx && idx < 0)
                throw new ArgumentOutOfRangeException(nameof(idx));
        }

        /// <summary>Returns null if not found (no throw).</summary>
        internal static object TryFindOnce(
            IHTMLDocument2 document,
            IDictionary<string, string> filters,
            int idx)
        {
            if (document == null)
                return null;

            object fast;
            if (TryFindByIdOnly(document, filters, idx, out fast))
                return fast;

            return PickMatch(EnumerateElements(document, filters), idx);
        }

        /// <summary>Returns null if not found under <paramref name="scopeElement"/> (no throw).</summary>
        internal static object TryFindOnceInScope(
            object scopeElement,
            IDictionary<string, string> filters,
            int idx)
        {
            if (!ComElementHelper.IsValidElement(scopeElement))
                return null;

            return TryFindInScope(scopeElement, filters, idx);
        }

        /// <summary>Returns null if not found yet (no throw).</summary>
        private static object TryFind(IHTMLDocument2 document, IDictionary<string, string> filters, int idx) =>
            TryFindOnce(document, filters, idx);

        private static object TryFindInScope(object scopeElement, IDictionary<string, string> filters, int idx)
        {
            return PickMatch(EnumerateElementsInScope(scopeElement, filters), idx);
        }

        private static object[] TryFindAll(IHTMLDocument2 document, IDictionary<string, string> filters)
        {
            object[] byId;
            if (TryFindAllByIdOnly(document, filters, out byId))
                return byId;

            return ToArrayOrNull(EnumerateElements(document, filters));
        }

        private static object[] TryFindAllInScope(object scopeElement, IDictionary<string, string> filters)
        {
            return ToArrayOrNull(EnumerateElementsInScope(scopeElement, filters));
        }

        private static object[] ToArrayOrNull(IEnumerable<object> matches)
        {
            var list = new List<object>();
            foreach (var el in matches)
                list.Add(el);

            return list.Count == 0 ? null : list.ToArray();
        }

        private static bool TryFindAllByIdOnly(
            IHTMLDocument2 document,
            IDictionary<string, string> filters,
            out object[] elements)
        {
            elements = null;
            if (filters.Count != 1)
                return false;

            string id;
            if (!filters.TryGetValue(ElementLocatorKeys.Id, out id) || string.IsNullOrEmpty(id))
                return false;

            try
            {
                dynamic doc = document;
                var element = doc.getElementById(id);
                if (!ComElementHelper.IsValidElement(element))
                    return true;

                elements = new[] { element };
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static object PickMatch(IEnumerable<object> matches, int idx)
        {
            var list = new List<object>();
            foreach (var el in matches)
                list.Add(el);

            if (list.Count <= idx)
                return null;

            return list[idx];
        }

        private static bool TryFindByIdOnly(IHTMLDocument2 document, IDictionary<string, string> filters, int idx, out object element)
        {
            element = null;
            if (idx != 0 || filters.Count != 1)
                return false;

            string id;
            if (!filters.TryGetValue(ElementLocatorKeys.Id, out id) || string.IsNullOrEmpty(id))
                return false;

            try
            {
                dynamic doc = document;
                element = doc.getElementById(id);
                return ComElementHelper.IsValidElement(element);
            }
            catch
            {
                return false;
            }
        }

        private static IEnumerable<object> EnumerateElements(IHTMLDocument2 document, IDictionary<string, string> filters)
        {
            dynamic doc = document;
            return EnumerateCollection(doc, filters);
        }

        private static IEnumerable<object> EnumerateElementsInScope(object scopeElement, IDictionary<string, string> filters)
        {
            dynamic root = scopeElement;
            return EnumerateCollection(root, filters);
        }

        private static IEnumerable<object> EnumerateCollection(dynamic root, IDictionary<string, string> filters)
        {
            string tagFilter;
            filters.TryGetValue(ElementLocatorKeys.Tag, out tagFilter);

            dynamic collection;
            if (!string.IsNullOrEmpty(tagFilter))
            {
                try { collection = root.getElementsByTagName(tagFilter); }
                catch { yield break; }
            }
            else
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

                if (!ComElementHelper.IsValidElement(el))
                    continue;

                if (ElementMatches(el, filters))
                    yield return el;
            }
        }

        private static bool ElementMatches(object element, IDictionary<string, string> filters)
        {
            foreach (var kv in filters)
            {
                if (kv.Key.Equals(ElementLocatorKeys.Id, StringComparison.OrdinalIgnoreCase))
                {
                    if (!StringEquals(GetStringProp(element, "id"), kv.Value))
                        return false;
                    continue;
                }

                if (kv.Key.Equals(ElementLocatorKeys.Tag, StringComparison.OrdinalIgnoreCase))
                {
                    if (!StringEquals(GetStringProp(element, "tagName"), kv.Value))
                        return false;
                    continue;
                }

                if (kv.Key.Equals(ElementLocatorKeys.Name, StringComparison.OrdinalIgnoreCase))
                {
                    if (!StringEquals(GetStringProp(element, "name"), kv.Value))
                        return false;
                    continue;
                }

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

            return true;
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

        private static string DescribeFilters(IDictionary<string, string> filters)
        {
            var sb = new StringBuilder();
            foreach (var kv in filters)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(kv.Key).Append('=').Append(kv.Value);
            }
            return sb.ToString();
        }
    }
}
