using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using FlaUI.Inspector.Models;

namespace FlaUI.Inspector.Services
{
    public static class SelectorXmlSerializer
    {
        private const string RegexSuffix = "-re";

        private static readonly Dictionary<string, string> AttributeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "ControlType", "role" },
            { "Title", "title" },
            { "Name", "name" },
            { "AutomationId", "automationid" },
            { "ClassName", "cls" },
            { "FrameworkId", "framework" },
            { "ProcessName", "app" },
            { "FileName", "filename" },
            { "IndexInParent", "idx" }
        };

        private static readonly Dictionary<string, string> ReverseAttributeMap =
            AttributeMap.ToDictionary(kvp => kvp.Value, kvp => kvp.Key, StringComparer.OrdinalIgnoreCase);

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

                var value = property.Name == "ControlType"
                    ? FormatRole(property.Value)
                    : property.Value;

                attributes.Add(attrName + "='" + EscapeValue(value) + "'");
            }

            var body = attributes.Count > 0 ? " " + string.Join(" ", attributes) : string.Empty;
            return "<" + level.TagName + body + " />";
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

            if (!string.Equals(element.Name.LocalName, "wnd", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(element.Name.LocalName, "ctrl", StringComparison.OrdinalIgnoreCase))
                return null;

            var level = new SelectorLevel(element.Name.LocalName.ToLowerInvariant());
            foreach (var attribute in element.Attributes())
            {
                var attrName = attribute.Name.LocalName;
                var isRegex = attrName.EndsWith(RegexSuffix, StringComparison.OrdinalIgnoreCase);
                if (isRegex)
                    attrName = attrName.Substring(0, attrName.Length - RegexSuffix.Length);

                if (!ReverseAttributeMap.TryGetValue(attrName, out var propertyName))
                {
                    if (string.Equals(element.Name.LocalName, "wnd", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(attrName, "name", StringComparison.OrdinalIgnoreCase))
                    {
                        propertyName = "Title";
                    }
                    else
                    {
                        continue;
                    }
                }

                var value = UnescapeValue(attribute.Value);
                if (propertyName == "ControlType")
                    value = ParseRole(value);

                level.Properties.Add(new ElementPropertyItem
                {
                    Name = propertyName,
                    Value = value,
                    IsRegex = isRegex && ElementPropertyItem.SupportsRegexProperty(propertyName),
                    IsSelected = true,
                    CanToggle = true
                });
            }

            level.RefreshTagLine();
            return level;
        }

        private static string FormatRole(string controlType)
        {
            if (string.IsNullOrEmpty(controlType))
                return controlType;

            var chars = controlType.ToCharArray();
            if (chars.Length == 0)
                return controlType;

            var result = char.ToLowerInvariant(chars[0]).ToString();
            for (var i = 1; i < chars.Length; i++)
            {
                if (char.IsUpper(chars[i]))
                    result += " " + char.ToLowerInvariant(chars[i]);
                else
                    result += chars[i];
            }

            return result;
        }

        private static string ParseRole(string role)
        {
            if (string.IsNullOrEmpty(role))
                return role;

            var parts = role.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Concat(parts.Select(part =>
            {
                if (part.Length == 0)
                    return part;
                return char.ToUpperInvariant(part[0]) + (part.Length > 1 ? part.Substring(1) : string.Empty);
            }));
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
