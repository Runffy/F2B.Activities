using System;
using System.Collections.Generic;
using System.Linq;
using F2B.Browser.Chromium.Bridge.Selectors;
using F2B.Browser.Chromium.Inspector.Models;

namespace F2B.Browser.Chromium.Inspector.Services
{
    public static class InspectorSelectorSerializer
    {
        private const string RegexSuffix = "-re";

        public static string Serialize(IEnumerable<InspectorSelectorLevel> levels)
        {
            if (levels == null)
                return string.Empty;

            return string.Join(
                "\r\n",
                levels.Where(level => level != null && level.IsEnabled).Select(SerializeLevelTag));
        }

        public static string SerializeLevelTag(InspectorSelectorLevel level)
        {
            if (level == null)
                return string.Empty;

            var attrs = new List<string>();
            foreach (var property in level.Properties.Where(item => item.IsSelected && !string.IsNullOrEmpty(item.Value)))
            {
                var attrName = ToHtmlAttributeName(level.TagName, property.Name);
                if (string.IsNullOrEmpty(attrName))
                    continue;

                if (property.IsRegex && property.SupportsRegex)
                    attrName += RegexSuffix;

                attrs.Add(attrName + "='" + EscapeValue(property.Value) + "'");
            }

            return "<" + level.TagName + (attrs.Count == 0 ? " />" : " " + string.Join(" ", attrs) + " />");
        }

        public static IList<InspectorSelectorLevel> FromBridgeLevels(IEnumerable<SelectorLevel> levels)
        {
            return levels == null
                ? new List<InspectorSelectorLevel>()
                : levels.Select(InspectorSelectorLevel.FromSelectorLevel).ToList();
        }

        private static string ToHtmlAttributeName(string tagName, string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
                return string.Empty;

            switch (propertyName)
            {
                case "AutomationId":
                    return "id";
                case "ClassName":
                    return "class";
                case "TagName":
                    return "tag";
                case "IndexInParent":
                    return "idx";
                case "ControlType":
                case "role":
                    return string.Empty;
            }

            if (string.Equals(tagName, "frm", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(propertyName, "Url", StringComparison.OrdinalIgnoreCase))
                return "src";

            return propertyName;
        }

        private static string EscapeValue(string value)
        {
            return (value ?? string.Empty).Replace("'", "&apos;");
        }
    }
}
