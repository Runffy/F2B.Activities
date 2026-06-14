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
            return BuildLevelTag(level, selectedOnly: true);
        }

        /// <summary>
        /// Display label in Selector Editor list. Includes tag/id/class hints even when the level is disabled.
        /// </summary>
        public static string SerializeLevelDisplayTag(InspectorSelectorLevel level)
        {
            return BuildLevelTag(level, selectedOnly: false);
        }

        private static string BuildLevelTag(InspectorSelectorLevel level, bool selectedOnly)
        {
            if (level == null)
                return string.Empty;

            var attrs = new List<string>();
            foreach (var property in level.Properties.Where(item => item.IsSelected && !string.IsNullOrEmpty(item.Value)))
            {
                AppendAttribute(level, property, attrs);
            }

            if (selectedOnly &&
                string.Equals(level.TagName, "ctrl", StringComparison.OrdinalIgnoreCase))
            {
                EnsureMinimumCtrlAttributes(level, attrs);
            }

            if (!selectedOnly && attrs.Count == 0)
            {
                var hintNames = new[] { "tag", "id", "class", "text", "name", "type", "placeholder", "idx" };
                foreach (var hintName in hintNames)
                {
                    var property = level.Properties.FirstOrDefault(item =>
                        string.Equals(item.Name, hintName, StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrEmpty(item.Value));

                    if (property == null)
                        continue;

                    AppendAttribute(level, property, attrs);
                    if (attrs.Count >= 2)
                        break;
                }
            }

            return "<" + level.TagName + (attrs.Count == 0 ? " />" : " " + string.Join(" ", attrs) + " />");
        }

        private static void AppendAttribute(InspectorSelectorLevel level, InspectorPropertyItem property, IList<string> attrs)
        {
            var attrName = ToHtmlAttributeName(level.TagName, property.Name);
            if (string.IsNullOrEmpty(attrName))
                return;

            if (property.IsRegex && property.SupportsRegex)
                attrName += RegexSuffix;

            attrs.Add(attrName + "='" + EscapeValue(property.Value) + "'");
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
