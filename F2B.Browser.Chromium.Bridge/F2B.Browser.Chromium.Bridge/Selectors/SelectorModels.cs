using System;
using System.Collections.Generic;

namespace F2B.Browser.Chromium.Bridge.Selectors
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
                return false;

            if (propertyName.StartsWith("data-", StringComparison.OrdinalIgnoreCase))
                return true;

            switch (propertyName)
            {
                case "Name":
                case "name":
                case "Title":
                case "title":
                case "AutomationId":
                case "id":
                case "ClassName":
                case "class":
                case "ProcessName":
                case "FrameworkId":
                case "Url":
                case "url":
                case "src":
                case "TagName":
                case "tag":
                case "Href":
                case "href":
                case "Type":
                case "type":
                case "placeholder":
                case "text":
                case "value":
                case "style":
                case "aria-label":
                    return true;
                default:
                    return false;
            }
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
