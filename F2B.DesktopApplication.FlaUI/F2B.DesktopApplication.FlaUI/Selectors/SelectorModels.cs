using System.Collections.Generic;

namespace F2B.DesktopApplication.FlaUI.Selectors
{
    public sealed class SelectorProperty
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public bool IsSelected { get; set; } = true;
        public bool IsRegex { get; set; }

        public static bool SupportsRegexProperty(string propertyName)
        {
            switch (propertyName)
            {
                case "Name":
                case "Title":
                case "AutomationId":
                case "ClassName":
                case "ProcessName":
                case "FrameworkId":
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
        public List<SelectorProperty> Properties { get; }
    }
}
