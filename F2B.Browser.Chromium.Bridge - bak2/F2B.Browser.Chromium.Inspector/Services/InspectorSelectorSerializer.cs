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
        public const int CompactPropertyMaxLength = 20;

        public static bool IsCompactPropertyValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            if (value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0)
                return false;

            return value.Length <= CompactPropertyMaxLength;
        }

        public static string FormatDisplayValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var singleLine = value
                .Replace("\r\n", " ")
                .Replace("\n", " ")
                .Replace("\r", " ")
                .Trim();

            if (singleLine.Length > CompactPropertyMaxLength)
                return singleLine.Substring(0, CompactPropertyMaxLength) + "...";

            return singleLine;
        }

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
            return BuildLevelTag(level, selectedOnly: true, truncateForDisplay: false);
        }

        /// <summary>
        /// Display label in Selector Editor list: all properties except idx, sorted by value length ascending.
        /// </summary>
        public static string SerializeLevelDisplayTag(InspectorSelectorLevel level)
        {
            if (level == null)
                return string.Empty;

            var attrs = new List<string>();
            var properties = level.Properties
                .Where(item => !string.IsNullOrEmpty(item.Value)
                    && !string.Equals(item.Name, "idx", StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.Value.Length)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var property in properties)
                AppendAttribute(level, property, attrs, truncateForDisplay: true);

            return "<" + level.TagName + (attrs.Count == 0 ? " />" : " " + string.Join(" ", attrs) + " />");
        }

        private static string BuildLevelTag(InspectorSelectorLevel level, bool selectedOnly, bool truncateForDisplay)
        {
            if (level == null)
                return string.Empty;

            var attrs = new List<string>();
            foreach (var property in level.Properties.Where(item => item.IsSelected && !string.IsNullOrEmpty(item.Value)))
            {
                AppendAttribute(level, property, attrs, truncateForDisplay);
            }

            if (selectedOnly &&
                string.Equals(level.TagName, "ctrl", StringComparison.OrdinalIgnoreCase))
            {
                EnsureMinimumCtrlAttributes(level, attrs);
            }

            return "<" + level.TagName + (attrs.Count == 0 ? " />" : " " + string.Join(" ", attrs) + " />");
        }

        private static void AppendAttribute(
            InspectorSelectorLevel level,
            InspectorPropertyItem property,
            IList<string> attrs,
            bool truncateForDisplay)
        {
            var attrName = ToHtmlAttributeName(level.TagName, property.Name);
            if (string.IsNullOrEmpty(attrName))
                return;

            if (property.IsRegex && property.SupportsRegex)
                attrName += RegexSuffix;

            var value = EscapeValue(truncateForDisplay
                ? FormatDisplayValue(property.Value)
                : property.Value);

            attrs.Add(attrName + "='" + value + "'");
        }

        private static void AppendAttribute(InspectorSelectorLevel level, InspectorPropertyItem property, IList<string> attrs)
        {
            AppendAttribute(level, property, attrs, truncateForDisplay: false);
        }

        private static void EnsureMinimumCtrlAttributes(InspectorSelectorLevel level, IList<string> attrs)
        {
            if (attrs.Count > 0)
                return;

            var tagProperty = level.Properties.FirstOrDefault(item =>
                string.Equals(item.Name, "tag", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(item.Value));

            if (tagProperty != null)
                AppendAttribute(level, tagProperty, attrs);

            var idxProperty = level.Properties.FirstOrDefault(item =>
                string.Equals(item.Name, "idx", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(item.Value));

            if (idxProperty != null)
                AppendAttribute(level, idxProperty, attrs);
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
