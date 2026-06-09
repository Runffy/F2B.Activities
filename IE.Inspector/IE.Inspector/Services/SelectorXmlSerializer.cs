using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using IE.Inspector.Models;

namespace IE.Inspector.Services
{
    public static class SelectorXmlSerializer
    {
        private const string RegexSuffix = "-re";

        private static readonly Dictionary<string, string> AttributeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "title", "title" },
            { "hwnd", "hwnd" },
            { "url", "url" },
            { "name", "name" },
            { "idx", "idx" },
            { "tag", "tag" },
            { "id", "id" },
            { "type", "type" },
            { "class", "class" },
            { "text", "text" },
            { "text_contains", "text_contains" },
            { "text_re", "text_re" },
            { "value", "value" },
            { "selector", "selector" },
            { "index", "idx" }
        };

        private static readonly Dictionary<string, string> ReverseAttributeMap =
            AttributeMap.GroupBy(kvp => kvp.Value, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Key, StringComparer.OrdinalIgnoreCase);

        public static string Serialize(IEnumerable<SelectorLevel> levels)
        {
            var lines = levels.Where(l => l.IsEnabled).Select(SerializeLevelTag);
            return string.Join(Environment.NewLine, lines);
        }

        public static string SerializeLevelTag(SelectorLevel level)
        {
            var attributes = new List<string>();
            foreach (var property in level.Properties.Where(p => p.IsSelected && !string.IsNullOrEmpty(p.Value)))
            {
                if (!AttributeMap.TryGetValue(property.Name, out var attrName))
                    attrName = property.Name.ToLowerInvariant();

                if (property.IsRegex && ElementPropertyItem.SupportsRegexProperty(property.Name))
                    attrName += RegexSuffix;

                attributes.Add(attrName + "='" + EscapeValue(property.Value) + "'");
            }

            var body = attributes.Count > 0 ? " " + string.Join(" ", attributes) : string.Empty;
            return "<" + NormalizeTagName(level.TagName) + body + " />";
        }

        public static IList<SelectorLevel> Deserialize(string xml)
        {
            var levels = new List<SelectorLevel>();
            if (string.IsNullOrWhiteSpace(xml))
                return levels;

            foreach (var line in xml.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                var level = ParseLevelTag(trimmed);
                if (level != null)
                    levels.Add(level);
            }

            return levels;
        }

        private static SelectorLevel ParseLevelTag(string line)
        {
            XElement element;
            try
            {
                element = XElement.Parse(line);
            }
            catch
            {
                return null;
            }

            var tagName = element.Name.LocalName;
            if (!IsSupportedTag(tagName))
                return null;

            var level = new SelectorLevel(NormalizeTagName(tagName));
            foreach (var attribute in element.Attributes())
            {
                var attrName = attribute.Name.LocalName;
                var isRegex = attrName.EndsWith(RegexSuffix, StringComparison.OrdinalIgnoreCase);
                if (isRegex)
                    attrName = attrName.Substring(0, attrName.Length - RegexSuffix.Length);

                if (!ReverseAttributeMap.TryGetValue(attrName, out var propertyName))
                    propertyName = attrName;

                if (string.Equals(propertyName, "idx", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(level.TagName, "ctrl", StringComparison.OrdinalIgnoreCase))
                {
                    propertyName = "index";
                }

                level.Properties.Add(new ElementPropertyItem
                {
                    Name = propertyName,
                    Value = UnescapeValue(attribute.Value),
                    IsRegex = isRegex && ElementPropertyItem.SupportsRegexProperty(propertyName),
                    IsSelected = true,
                    CanToggle = true
                });
            }

            level.RefreshTagLine();
            return level;
        }

        private static bool IsSupportedTag(string tagName)
        {
            return string.Equals(tagName, "wnd", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tagName, "frm", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tagName, "ctrl", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tagName, "elm", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeTagName(string tagName)
        {
            if (string.Equals(tagName, "elm", StringComparison.OrdinalIgnoreCase))
                return "ctrl";

            return tagName?.ToLowerInvariant() ?? "ctrl";
        }

        private static string EscapeValue(string value)
        {
            return value?.Replace("'", "&apos;") ?? string.Empty;
        }

        private static string UnescapeValue(string value)
        {
            return value?.Replace("&apos;", "'") ?? string.Empty;
        }
    }
}
