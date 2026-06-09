using System;
using System.Collections.Generic;
using System.Linq;
using IE.Inspector.Models;

namespace IE.Inspector.Services
{
    /// <summary>
    /// 将 Inspector XML selector 层级转换为 F2B.Browser.IExplore.COM 当前使用的 locator / frame_path。
    /// 待库侧支持 XML 后可移入 F2B.Browser.IExplore.COM。
    /// </summary>
    internal static class SelectorLocatorBuilder
    {
        public static IDictionary<string, object> BuildLocatorFromLevels(IEnumerable<SelectorLevel> levels)
        {
            var elementLevel = levels?.LastOrDefault(l =>
                l.IsEnabled && string.Equals(l.TagName, "ctrl", StringComparison.OrdinalIgnoreCase));

            return elementLevel == null
                ? new Dictionary<string, object>()
                : BuildLocatorFromLevel(elementLevel);
        }

        public static IList<object> BuildFramePathFromLevels(IEnumerable<SelectorLevel> levels)
        {
            var path = new List<object>();
            if (levels == null)
                return path;

            foreach (var level in levels.Where(l =>
                l.IsEnabled && string.Equals(l.TagName, "frm", StringComparison.OrdinalIgnoreCase)))
            {
                var indexProperty = level.Properties.FirstOrDefault(p =>
                    p.IsSelected && string.Equals(p.Name, "idx", StringComparison.OrdinalIgnoreCase));
                if (indexProperty != null && int.TryParse(indexProperty.Value, out var index))
                {
                    path.Add(index);
                    continue;
                }

                var nameProperty = level.Properties.FirstOrDefault(p =>
                    p.IsSelected && string.Equals(p.Name, "name", StringComparison.OrdinalIgnoreCase));
                if (nameProperty != null && !string.IsNullOrWhiteSpace(nameProperty.Value))
                    path.Add(nameProperty.Value);
            }

            return path;
        }

        public static IDictionary<string, object> BuildLocatorFromLevel(SelectorLevel level, bool includeIndex = true)
        {
            var locator = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (level == null)
                return locator;

            foreach (var property in level.Properties.Where(p => p.IsSelected && !string.IsNullOrEmpty(p.Value)))
            {
                if (!includeIndex && string.Equals(property.Name, "index", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (property.IsRegex)
                {
                    MapRegexLocatorKey(locator, property);
                    continue;
                }

                switch (property.Name)
                {
                    case "index":
                        if (int.TryParse(property.Value, out var index))
                            locator["index"] = index;
                        break;
                    case "class":
                        locator["class"] = property.Value;
                        break;
                    default:
                        locator[property.Name] = property.Value;
                        break;
                }
            }

            if (locator.ContainsKey("tag"))
            {
                var tag = locator["tag"]?.ToString();
                if (!string.IsNullOrEmpty(tag))
                    locator["tag"] = tag.ToLowerInvariant();
            }

            return locator;
        }

        private static void MapRegexLocatorKey(IDictionary<string, object> locator, ElementPropertyItem property)
        {
            switch (property.Name)
            {
                case "text":
                    locator["text_re"] = property.Value;
                    break;
                case "text_contains":
                    locator["text_contains"] = property.Value;
                    break;
                case "name":
                    locator["name"] = property.Value;
                    break;
                case "id":
                    locator["id"] = property.Value;
                    break;
                default:
                    locator[property.Name] = property.Value;
                    break;
            }
        }
    }
}
