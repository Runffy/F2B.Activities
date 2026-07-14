using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using F2B.Browser.Chromium.Cdp.Browser;
using F2B.Browser.Chromium.Cdp.Exceptions;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal static class CdpElementSelect
    {
        internal static void EnsureSelect(CdpElement element)
        {
            if (element == null)
            {
                throw new ArgumentNullException("element");
            }

            if (!string.Equals(element.Tag, "select", StringComparison.OrdinalIgnoreCase))
            {
                throw new BrowserException("Element is not a select.");
            }
        }

        internal static bool IsMultiSelect(CdpElement element)
        {
            if (element == null ||
                !string.Equals(element.Tag, "select", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return element.Attr("multiple") != null;
        }

        internal static CdpElement[] GetSelectOptions(CdpElement element)
        {
            EnsureSelect(element);
            return QueryOptions(element);
        }

        internal static CdpElement[] GetSelectedOptions(CdpElement element)
        {
            EnsureSelect(element);
            return QueryOptions(element).Where(option => option.States.IsSelected).ToArray();
        }

        internal static void Select(CdpElement element, CdpSelectBy by, IList<object> values, int timeoutMs)
        {
            EnsureSelect(element);
            if (values == null || values.Count == 0)
            {
                throw new ArgumentException("At least one value is required.", "values");
            }

            if (!IsMultiSelect(element) && values.Count > 1)
            {
                throw new BrowserException("Single select accepts only one value.");
            }

            var options = WaitForMatchingOptions(element, by, values, timeoutMs);
            if (options.Length == 0)
            {
                throw new BrowserException("Matching option was not found.");
            }

            if (!IsMultiSelect(element) && options.Length > 1)
            {
                options = new[] { options[0] };
            }

            SetOptionsSelected(element, options, true);
        }

        internal static void Unselect(CdpElement element, CdpSelectBy by, IList<object> values, int timeoutMs)
        {
            EnsureSelect(element);
            if (!IsMultiSelect(element))
            {
                throw new BrowserException("Unselect is only supported on multi select elements.");
            }

            if (values == null || values.Count == 0)
            {
                throw new ArgumentException("At least one value is required.", "values");
            }

            var options = WaitForMatchingOptions(element, by, values, timeoutMs);
            if (options.Length == 0)
            {
                throw new BrowserException("Matching option was not found.");
            }

            SetOptionsSelected(element, options, false);
        }

        internal static void SelectAll(CdpElement element)
        {
            EnsureSelect(element);
            if (!IsMultiSelect(element))
            {
                throw new BrowserException("SelectAll is only supported on multi select elements.");
            }

            SetOptionsSelected(element, QueryOptions(element), true);
        }

        internal static void UnselectAll(CdpElement element)
        {
            EnsureSelect(element);
            if (!IsMultiSelect(element))
            {
                throw new BrowserException("UnselectAll is only supported on multi select elements.");
            }

            SetOptionsSelected(element, QueryOptions(element), false);
        }

        private static CdpElement[] MatchOptions(CdpElement[] options, CdpSelectBy by, IList<object> values)
        {
            var matched = new List<CdpElement>();
            switch (by)
            {
                case CdpSelectBy.Text:
                    var textSet = new HashSet<string>(values.Select(Convert.ToString), StringComparer.Ordinal);
                    foreach (var option in options)
                    {
                        if (textSet.Contains(option.Text))
                        {
                            matched.Add(option);
                        }
                    }

                    break;

                case CdpSelectBy.Value:
                    var expectedValues = new HashSet<string>(values.Select(Convert.ToString), StringComparer.Ordinal);
                    foreach (var option in options)
                    {
                        if (expectedValues.Contains(option.Attr("value") ?? string.Empty))
                        {
                            matched.Add(option);
                        }
                    }

                    break;

                case CdpSelectBy.Index:
                    foreach (var value in values)
                    {
                        var index = Convert.ToInt32(value);
                        if (index < 0 || index >= options.Length)
                        {
                            continue;
                        }

                        matched.Add(options[index]);
                    }

                    break;

                default:
                    throw new ArgumentOutOfRangeException("by");
            }

            return matched.ToArray();
        }

        private static CdpElement[] WaitForMatchingOptions(
            CdpElement element,
            CdpSelectBy by,
            IList<object> values,
            int timeoutMs)
        {
            var requiredCount = GetRequiredMatchCount(by, values);
            var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(0, timeoutMs));

            do
            {
                var matched = MatchOptions(QueryOptions(element), by, values);
                if (matched.Length >= requiredCount)
                {
                    return matched;
                }

                if (timeoutMs <= 0)
                {
                    break;
                }

                Thread.Sleep(10);
            }
            while (DateTime.UtcNow < deadline);

            return new CdpElement[0];
        }

        private static int GetRequiredMatchCount(CdpSelectBy by, IList<object> values)
        {
            if (by == CdpSelectBy.Index)
            {
                var count = 0;
                foreach (var value in values)
                {
                    var index = Convert.ToInt32(value);
                    if (index >= 0)
                    {
                        count++;
                    }
                }

                return count;
            }

            return values.Count;
        }

        private static void SetOptionsSelected(CdpElement select, IList<CdpElement> options, bool selected)
        {
            if (options == null || options.Count == 0)
            {
                return;
            }

            var mode = selected ? "true" : "false";
            foreach (var option in options)
            {
                option.RunJs("this.selected=" + mode + ";");
            }

            select.RunJs("this.dispatchEvent(new Event('change', { bubbles: true }));");
        }

        private static CdpElement[] QueryOptions(CdpElement select)
        {
            select.Context.RefreshIds();
            var session = select.Context.Session;
            var response = session.Send("Runtime.callFunctionOn", new Dictionary<string, object>
            {
                {
                    "functionDeclaration",
                    "function() { return Array.prototype.slice.call(this.querySelectorAll('option')); }"
                },
                { "objectId", select.ObjectId },
                { "returnByValue", false }
            });

            var result = CdpValueConverter.GetDictionary(response, "result");
            var arrayObjectId = result != null ? CdpValueConverter.GetString(result, "objectId") : null;
            if (string.IsNullOrEmpty(arrayObjectId))
            {
                return new CdpElement[0];
            }

            try
            {
                var propsResponse = session.Send("Runtime.getProperties", new Dictionary<string, object>
                {
                    { "objectId", arrayObjectId },
                    { "ownProperties", true }
                });

                var props = CdpValueConverter.GetList(propsResponse, "result");
                var indexedElements = new List<KeyValuePair<int, CdpElement>>();
                if (props != null)
                {
                    foreach (var propEntry in props)
                    {
                        var prop = propEntry as Dictionary<string, object>;
                        if (prop == null)
                        {
                            continue;
                        }

                        var name = CdpValueConverter.GetString(prop, "name");
                        if (string.IsNullOrEmpty(name) || name == "length")
                        {
                            continue;
                        }

                        int index;
                        if (!int.TryParse(name, out index))
                        {
                            continue;
                        }

                        var value = CdpValueConverter.GetDictionary(prop, "value");
                        var objectId = value != null ? CdpValueConverter.GetString(value, "objectId") : null;
                        if (string.IsNullOrEmpty(objectId))
                        {
                            continue;
                        }

                        var element = ResolveOptionElement(session, select, objectId);
                        if (element != null)
                        {
                            indexedElements.Add(new KeyValuePair<int, CdpElement>(index, element));
                        }
                    }
                }

                indexedElements.Sort((left, right) => left.Key.CompareTo(right.Key));
                return indexedElements.Select(pair => pair.Value).ToArray();
            }
            finally
            {
                ReleaseRemoteObject(session, arrayObjectId);
            }
        }

        private static CdpElement ResolveOptionElement(CdpTabSession session, CdpElement select, string objectId)
        {
            var request = session.Send("DOM.requestNode", new Dictionary<string, object>
            {
                { "objectId", objectId }
            });

            var nodeId = CdpValueConverter.GetInt(request, "nodeId");
            if (nodeId <= 0)
            {
                return null;
            }

            var describe = session.Send("DOM.describeNode", new Dictionary<string, object>
            {
                { "nodeId", nodeId }
            });

            var node = CdpValueConverter.GetDictionary(describe, "node");
            if (node == null)
            {
                return null;
            }

            return new CdpElement(
                select.Tab,
                CdpValueConverter.GetString(node, "localName") ?? string.Empty,
                CdpValueConverter.GetInt(node, "backendNodeId"),
                nodeId,
                objectId);
        }

        private static void ReleaseRemoteObject(CdpTabSession session, string objectId)
        {
            try
            {
                session.Send("Runtime.releaseObject", new Dictionary<string, object>
                {
                    { "objectId", objectId }
                });
            }
            catch
            {
            }
        }
    }
}
