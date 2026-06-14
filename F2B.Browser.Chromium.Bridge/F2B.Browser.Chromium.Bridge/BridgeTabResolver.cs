using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using F2B.Browser.Chromium.Bridge.Selectors;

namespace F2B.Browser.Chromium.Bridge
{
    public sealed class BwTabMatch
    {
        public BwTabMatch(string instanceId, BwTab tab)
        {
            InstanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));
            Tab = tab ?? throw new ArgumentNullException(nameof(tab));
        }

        public string InstanceId { get; }

        public BwTab Tab { get; }
    }

    public static class BridgeTabResolver
    {
        public static bool TabMatchesWnd(BwTab tab, SelectorLevel wndLevel)
        {
            if (tab == null || wndLevel == null)
                return false;

            foreach (var property in wndLevel.Properties.Where(item => item.IsSelected))
            {
                if (IsWndIndexProperty(property.Name))
                    continue;

                if (!MatchTabProperty(tab, property))
                    return false;
            }

            return true;
        }

        public static IList<BwTabMatch> OrderMatches(IEnumerable<BwTabMatch> matches)
        {
            return matches
                .OrderBy(item => item.InstanceId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Tab.WindowId)
                .ThenBy(item => item.Tab.TabId)
                .ToList();
        }

        /// <summary>
        /// Picks one tab from wnd matches. Supports optional idx on the wnd level.
        /// </summary>
        public static BwTabMatch SelectMatch(IList<BwTabMatch> matches, SelectorLevel wndLevel)
        {
            if (matches == null || matches.Count == 0)
                return null;

            var ordered = OrderMatches(matches);

            if (TryGetWndIndex(wndLevel, out var explicitIndex))
            {
                if (explicitIndex < 0 || explicitIndex >= ordered.Count)
                {
                    throw new InvalidOperationException(
                        "Wnd selector idx " + explicitIndex + " is out of range. Matched tab count: " + ordered.Count + ".");
                }

                return ordered[explicitIndex];
            }

            if (ordered.Count == 1)
                return ordered[0];

            var activeMatches = ordered.Where(item => item.Tab.Active).ToList();
            if (activeMatches.Count == 1)
                return activeMatches[0];

            if (activeMatches.Count > 1)
                return activeMatches[0];

            return ordered[0];
        }

        public static void EnsureWndOnlySelector(string selectorXml)
        {
            if (string.IsNullOrWhiteSpace(selectorXml))
                throw new ArgumentException("Attach Browser requires a non-empty selector XML with <wnd>.");

            var levels = SelectorXmlSerializer.Deserialize(selectorXml);
            if (levels.Count == 0 ||
                !string.Equals(levels[0].TagName, "wnd", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Attach Browser selector must start with a <wnd> level.");
            }

            for (var i = 1; i < levels.Count; i++)
            {
                if (levels[i].IsEnabled)
                {
                    throw new ArgumentException(
                        "Attach Browser selector must contain <wnd> only. Remove level: <" + levels[i].TagName + ">.");
                }
            }
        }

        private static bool IsWndIndexProperty(string propertyName)
        {
            return string.Equals(propertyName, "IndexInParent", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(propertyName, "idx", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetWndIndex(SelectorLevel wndLevel, out int index)
        {
            index = -1;
            if (wndLevel == null)
                return false;

            var indexProperty = wndLevel.Properties.FirstOrDefault(item =>
                item.IsSelected && IsWndIndexProperty(item.Name));

            return indexProperty != null &&
                   int.TryParse(indexProperty.Value, out index);
        }

        private static bool MatchTabProperty(BwTab tab, SelectorProperty property)
        {
            var expected = property.Value ?? string.Empty;
            string actual;
            var name = property.Name ?? string.Empty;

            switch (name)
            {
                case "Title":
                case "title":
                case "Name":
                case "name":
                    actual = tab.Title ?? string.Empty;
                    break;
                case "Url":
                case "url":
                    actual = tab.Url ?? string.Empty;
                    break;
                default:
                    return true;
            }

            if (property.IsRegex)
            {
                try
                {
                    return Regex.IsMatch(actual, expected, RegexOptions.IgnoreCase);
                }
                catch
                {
                    return false;
                }
            }

            return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}
