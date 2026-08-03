using System;
using System.Collections.Generic;

namespace F2B.Browser.Chromium.Cdp.Selectors
{
    public sealed class SelectorProperty
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public bool IsSelected { get; set; } = true;
        public bool IsRegex { get; set; }

        public static bool SupportsRegexProperty(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                return false;
            }

            // Structural selector fields, not matchable HTML attributes.
            if (string.Equals(propertyName, "idx", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(propertyName, "level", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }
    }

    public sealed class SelectorLevel
    {
        public SelectorLevel(string tagName)
        {
            TagName = tagName ?? "ctrl";
            Properties = new List<SelectorProperty>();
        }

        public string TagName { get; }
        public bool IsEnabled { get; set; } = true;
        public bool CanDisable { get; set; } = true;
        public List<SelectorProperty> Properties { get; }
    }
}
