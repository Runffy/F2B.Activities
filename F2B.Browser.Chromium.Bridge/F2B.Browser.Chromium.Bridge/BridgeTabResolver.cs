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
                if (property.Name == "IndexInParent")
                    continue;

                if (!MatchTabProperty(tab, property))
                    return false;
            }

            return true;
        }

        public static BwTabMatch SelectMatch(IList<BwTabMatch> matches, SelectorLevel wndLevel)
        {
            if (matches == null || matches.Count == 0)
                return null;

            var ordered = matches
                .OrderBy(item => item.Tab.TabId)
                .ToList();

            var indexProperty = wndLevel?.Properties
                .FirstOrDefault(item => item.IsSelected && item.Name == "IndexInParent");

            if (indexProperty != null &&
                int.TryParse(indexProperty.Value, out var explicitIndex) &&
                explicitIndex >= 0 &&
                explicitIndex < ordered.Count)
            {
                return ordered[explicitIndex];
            }

            if (ordered.Count == 1)
                return ordered[0];

            var activeMatch = ordered.FirstOrDefault(item => item.Tab.Active);
            if (activeMatch != null)
                return activeMatch;

            return ordered[ordered.Count - 1];
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
                case "IndexInParent":
                case "idx":
                    return true;
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
