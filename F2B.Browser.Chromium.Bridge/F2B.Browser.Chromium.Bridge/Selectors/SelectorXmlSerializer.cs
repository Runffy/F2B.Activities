using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace F2B.Browser.Chromium.Bridge.Selectors
{
    /// <summary>
    /// Inspector-compatible selector XML (one self-closing tag per line).
    /// Web extensions: url, tag, type, href on ctrl/wnd levels.
    /// </summary>
    public static class SelectorXmlSerializer
    {
        private const string RegexSuffix = "-re";

        private static readonly Dictionary<string, string> AttributeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "ControlType", "role" },
            { "AutomationId", "automationid" },
            { "ClassName", "cls" },
            { "TagName", "tag" },
            { "IndexInParent", "idx" },
            { "Title", "title" },
            { "Url", "url" },
            { "Name", "name" },
            { "Type", "type" },
            { "Href", "href" },
            { "text", "text" },
            { "id", "id" },
            { "class", "class" },
            { "tag", "tag" },
            { "idx", "idx" }
        };

        private static readonly Dictionary<string, string> WireToHtmlMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "automationid", "id" },
            { "cls", "class" },
            { "id", "id" },
            { "class", "class" },
            { "tag", "tag" },
            { "idx", "idx" },
            { "name", "name" },
            { "title", "title" },
            { "url", "url" },
            { "type", "type" },
            { "href", "href" },
            { "text", "text" }
        };

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

        public static bool HasWndLevel(string selectorXml)
        {
            var levels = Deserialize(selectorXml);
            return levels.Count > 0 &&
                   string.Equals(levels[0].TagName, "wnd", StringComparison.OrdinalIgnoreCase);
        }

        public static SelectorScope SplitScope(string selectorXml)
        {
            var levels = Deserialize(selectorXml);
            if (levels.Count == 0)
                return new SelectorScope(null, new List<SelectorLevel>(), levels);

            var index = 0;
            SelectorLevel tabLevel = null;

            if (string.Equals(levels[0].TagName, "wnd", StringComparison.OrdinalIgnoreCase))
            {
                tabLevel = levels[0];
                index = 1;
            }

            var frameLevels = new List<SelectorLevel>();
            while (index < levels.Count &&
                   string.Equals(levels[index].TagName, "frm", StringComparison.OrdinalIgnoreCase))
            {
                frameLevels.Add(levels[index]);
                index++;
            }

            var elementLevels = levels.Skip(index).ToList();
            return new SelectorScope(tabLevel, frameLevels, elementLevels);
        }

        /// <summary>
        /// Rebuild selector XML without &lt;wnd&gt; (frm + ctrl levels only).
        /// </summary>
        public static string ToOperationXml(SelectorScope scope)
        {
            if (scope == null)
                return string.Empty;

            var lines = new List<string>();
            foreach (var level in scope.FrameLevels)
                lines.Add(SerializeLevel(level));

            foreach (var level in scope.ElementLevels)
                lines.Add(SerializeLevel(level));

            return string.Join("\r\n", lines);
        }

        public static string Serialize(IEnumerable<SelectorLevel> levels)
        {
            if (levels == null)
                return string.Empty;

            return string.Join(
                "\r\n",
                levels.Where(level => level != null && level.IsEnabled).Select(SerializeLevel));
        }

        public static string SerializeLevelTag(SelectorLevel level)
        {
            return SerializeLevel(level);
        }

        private static string SerializeLevel(SelectorLevel level)
        {
            if (level == null)
                return string.Empty;

            var attrs = new List<string>();
            foreach (var property in level.Properties.Where(item => item.IsSelected))
            {
                if (string.Equals(property.Name, "role", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(property.Name, "ControlType", StringComparison.OrdinalIgnoreCase))
                    continue;

                var attrName = ResolveWireAttributeName(level.TagName, property.Name);
                if (string.IsNullOrEmpty(attrName))
                    continue;

                if (property.IsRegex && SelectorProperty.SupportsRegexProperty(property.Name))
                    attrName += RegexSuffix;

                attrs.Add(attrName + "='" + EscapeValue(property.Value) + "'");
            }

            if (string.Equals(level.TagName, "ctrl", StringComparison.OrdinalIgnoreCase))
            {
                EnsureMinimumCtrlAttributes(level, attrs);
            }

            if (string.Equals(level.TagName, "ctrl", StringComparison.OrdinalIgnoreCase) &&
                !attrs.Any(item => item.StartsWith("role=", StringComparison.OrdinalIgnoreCase) ||
                                   item.StartsWith("role-re=", StringComparison.OrdinalIgnoreCase)))
            {
                var tag = level.Properties.FirstOrDefault(item =>
                    string.Equals(item.Name, "tag", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(item.Name, "TagName", StringComparison.OrdinalIgnoreCase))?.Value;
                var type = level.Properties.FirstOrDefault(item =>
                    string.Equals(item.Name, "type", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(item.Name, "Type", StringComparison.OrdinalIgnoreCase))?.Value;
                var role = DeriveRoleFromTag(tag, type);
                if (!string.IsNullOrEmpty(role))
                    attrs.Insert(0, "role='" + EscapeValue(role) + "'");
            }

            return "<" + level.TagName + (attrs.Count == 0 ? " />" : " " + string.Join(" ", attrs) + " />");
        }

        private static void EnsureMinimumCtrlAttributes(SelectorLevel level, IList<string> attrs)
        {
            if (attrs.Count > 0)
                return;

            var tagProperty = level.Properties.FirstOrDefault(item =>
                (string.Equals(item.Name, "tag", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(item.Name, "TagName", StringComparison.OrdinalIgnoreCase)) &&
                !string.IsNullOrEmpty(item.Value));

            if (tagProperty != null)
            {
                var tagAttr = ResolveWireAttributeName(level.TagName, tagProperty.Name);
                if (!string.IsNullOrEmpty(tagAttr))
                    attrs.Add(tagAttr + "='" + EscapeValue(tagProperty.Value) + "'");
            }

            var idxProperty = level.Properties.FirstOrDefault(item =>
                (string.Equals(item.Name, "idx", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(item.Name, "IndexInParent", StringComparison.OrdinalIgnoreCase)) &&
                !string.IsNullOrEmpty(item.Value));

            if (idxProperty != null)
            {
                var idxAttr = ResolveWireAttributeName(level.TagName, idxProperty.Name);
                if (!string.IsNullOrEmpty(idxAttr))
                    attrs.Add(idxAttr + "='" + EscapeValue(idxProperty.Value) + "'");
            }
        }

        private static string DeriveRoleFromTag(string tag, string type)
        {
            tag = (tag ?? string.Empty).Trim().ToLowerInvariant();
            type = (type ?? string.Empty).Trim().ToLowerInvariant();

            if (tag == "button")
                return "button";
            if (tag == "textarea")
                return "edit";
            if (tag == "select")
                return "combobox";
            if (tag == "a")
                return "link";
            if (tag == "img")
                return "image";
            if (tag == "iframe" || tag == "frame")
                return "frm";
            if (tag == "input")
            {
                if (type == "checkbox")
                    return "checkbox";
                if (type == "radio")
                    return "radiobutton";
                if (type == "button" || type == "submit" || type == "reset" || type == "image")
                    return "button";
                return "edit";
            }

            if (tag == "li")
                return "listitem";
            if (tag == "ul" || tag == "ol")
                return "list";

            return "pane";
        }

        private static string EscapeValue(string value)
        {
            return (value ?? string.Empty).Replace("'", "&apos;");
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
                !string.Equals(element.Name.LocalName, "frm", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(element.Name.LocalName, "ctrl", StringComparison.OrdinalIgnoreCase))
                return null;

            var level = new SelectorLevel(element.Name.LocalName.ToLowerInvariant());
            foreach (var attribute in element.Attributes())
            {
                var attrName = attribute.Name.LocalName;
                var isRegex = attrName.EndsWith(RegexSuffix, StringComparison.OrdinalIgnoreCase);
                if (isRegex)
                    attrName = attrName.Substring(0, attrName.Length - RegexSuffix.Length);

                if (string.Equals(attrName, "role", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!WireToHtmlMap.TryGetValue(attrName, out var propertyName))
                {
                    if (string.Equals(attrName, "automationid", StringComparison.OrdinalIgnoreCase))
                    {
                        propertyName = "id";
                    }
                    else if (string.Equals(attrName, "cls", StringComparison.OrdinalIgnoreCase))
                    {
                        propertyName = "class";
                    }
                    else if (string.Equals(element.Name.LocalName, "frm", StringComparison.OrdinalIgnoreCase) &&
                             string.Equals(attrName, "url", StringComparison.OrdinalIgnoreCase))
                    {
                        propertyName = "src";
                    }
                    else if (string.Equals(element.Name.LocalName, "wnd", StringComparison.OrdinalIgnoreCase) &&
                             string.Equals(attrName, "name", StringComparison.OrdinalIgnoreCase))
                    {
                        propertyName = "title";
                    }
                    else
                    {
                        propertyName = attrName;
                    }
                }

                var value = UnescapeValue(attribute.Value);

                level.Properties.Add(new SelectorProperty
                {
                    Name = propertyName,
                    Value = value,
                    IsRegex = isRegex && SelectorProperty.SupportsRegexProperty(propertyName),
                    IsSelected = true
                });
            }

            return level;
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

        private static string UnescapeValue(string value)
        {
            return value?.Replace("&apos;", "'") ?? string.Empty;
        }

        private static string ResolveWireAttributeName(string tagName, string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
                return string.Empty;

            if (string.Equals(tagName, "frm", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(propertyName, "src", StringComparison.OrdinalIgnoreCase))
                return "url";

            if (AttributeMap.TryGetValue(propertyName, out var mapped))
                return mapped;

            return propertyName;
        }

        private static string ResolveAttributeName(string propertyName)
        {
            return ResolveWireAttributeName(null, propertyName);
        }
    }
}
