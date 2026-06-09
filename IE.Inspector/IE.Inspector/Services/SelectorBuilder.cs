using System;
using System.Collections.Generic;
using System.Linq;
using F2B.Browser.IExplore.COM;
using IE.Inspector.Models;

namespace IE.Inspector.Services
{
    public static class SelectorBuilder
    {
        private static readonly string[] ElementPropertyPriority =
        {
            "selector", "id", "name", "tag", "type", "class", "text", "value", "index"
        };

        private static readonly HashSet<string> ExcludedAncestorTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "head"
        };

        public static IList<SelectorLevel> BuildLevels(
            IEWindowController controller,
            IEWindowController.IEDomElement target,
            IList<object> framePath)
        {
            var levels = new List<SelectorLevel>();
            if (controller == null || target == null)
                return levels;

            var windowLevel = new SelectorLevel("wnd") { CanDisable = false, IsEnabled = true };
            AddProperty(windowLevel, "title", controller.title, true, true);
            AddProperty(windowLevel, "url", controller.url, false, true);
            levels.Add(windowLevel);

            framePath = framePath ?? target.build_frame_path() ?? new List<object>();
            foreach (var frameRef in framePath)
            {
                var frameLevel = new SelectorLevel("frm") { CanDisable = true, IsEnabled = true };
                if (frameRef is int index)
                    AddProperty(frameLevel, "idx", index.ToString(), true, true);
                else
                    AddProperty(frameLevel, "name", frameRef?.ToString(), true, true);

                levels.Add(frameLevel);
            }

            var domPath = BuildDomSelectorPath(target);
            IEWindowController.IEDomElement scope = null;
            for (var i = 0; i < domPath.Count; i++)
            {
                var element = domPath[i];
                var isTarget = ReferenceEquals(element.raw, target.raw) || i == domPath.Count - 1;
                var tag = GetElementTag(element);

                if (IsFrameTag(tag) && !FramePathContains(levels, element))
                {
                    var frameLevel = new SelectorLevel("frm") { CanDisable = true, IsEnabled = true };
                    var frameName = ReadAttribute(element, "name");
                    var frameId = ReadAttribute(element, "id");
                    if (!string.IsNullOrEmpty(frameName))
                        AddProperty(frameLevel, "name", frameName, true, true);
                    else if (!string.IsNullOrEmpty(frameId))
                        AddProperty(frameLevel, "name", frameId, true, true);
                    else
                        AddProperty(frameLevel, "idx", GetFrameIndexAmongSiblings(element).ToString(), true, true);

                    levels.Add(frameLevel);
                    continue;
                }

                if (IsFrameTag(tag))
                    continue;

                var ctrlLevel = new SelectorLevel("ctrl");
                if (isTarget)
                {
                    PopulateElementProperties(ctrlLevel, element, controller, framePath, scope, preferUnique: true);
                    ConfigureCtrlLevelEnabled(ctrlLevel, isTarget: true);
                }
                else
                {
                    PopulateDisplayProperties(ctrlLevel, element);
                    if (ctrlLevel.Properties.Count == 0)
                        continue;
                    ConfigureCtrlLevelEnabled(ctrlLevel, isTarget: false);
                }

                levels.Add(ctrlLevel);
                if (ctrlLevel.IsEnabled)
                    scope = ResolveScopeElement(controller, framePath, scope, ctrlLevel) ?? scope;
            }

            EnsureTargetCtrlLevel(levels, controller, target, framePath);
            return levels;
        }

        /// <summary>
        /// 从文档根到目标的完整 DOM 路径（类似 UiPath UI Explorer 的 Visual/Selector 树）。
        /// </summary>
        public static IList<IEWindowController.IEDomElement> BuildDomSelectorPath(IEWindowController.IEDomElement target)
        {
            if (target == null)
                return new List<IEWindowController.IEDomElement>();

            var path = DomTreeBuilder.BuildPathToDocumentRoot(target);
            var filtered = new List<IEWindowController.IEDomElement>();
            foreach (var element in path)
            {
                if (element == null || !IsSelectorPathNode(element))
                    continue;

                var tag = GetElementTag(element);
                if (!string.IsNullOrWhiteSpace(tag) && ExcludedAncestorTags.Contains(tag))
                    continue;

                if (IsFrameTag(tag))
                    continue;

                filtered.Add(element);
            }

            if (filtered.Count == 0 || !filtered.Any(el => ReferenceEquals(el.raw, target.raw)))
                filtered.Add(target);

            return filtered;
        }

        private static void ConfigureCtrlLevelEnabled(SelectorLevel level, bool isTarget)
        {
            if (isTarget)
            {
                level.IsEnabled = true;
                level.CanDisable = false;
                return;
            }

            level.IsEnabled = false;
            level.CanDisable = true;
        }

        private static void PopulateDisplayProperties(SelectorLevel level, IEWindowController.IEDomElement element)
        {
            var tag = GetElementTag(element);
            if (string.IsNullOrWhiteSpace(tag))
                return;

            AddProperty(level, "tag", tag, true, true);
            AddProperty(level, "id", ReadAttribute(element, "id"), false, true);
            AddProperty(level, "name", ReadAttribute(element, "name"), false, true);
            AddProperty(level, "type", ReadAttribute(element, "type"), false, true);
            AddProperty(level, "class", ReadAttribute(element, "className"), false, true);
            AddProperty(level, "text", element.get_text(), false, true);
            AddProperty(level, "value", ReadAttribute(element, "value"), false, true);

            var index = GetIndexAmongSameTagSiblings(element);
            if (index >= 0)
                AddProperty(level, "index", index.ToString(), false, true);
        }

        private static void EnsureTargetCtrlLevel(
            IList<SelectorLevel> levels,
            IEWindowController controller,
            IEWindowController.IEDomElement target,
            IList<object> framePath)
        {
            if (levels.Any(level => string.Equals(level.TagName, "ctrl", StringComparison.OrdinalIgnoreCase)
                && level.IsEnabled))
            {
                return;
            }

            var targetLevel = new SelectorLevel("ctrl");
            PopulateElementProperties(targetLevel, target, controller, framePath, scope: null, preferUnique: true);
            ConfigureCtrlLevelEnabled(targetLevel, isTarget: true);
            if (targetLevel.Properties.Count > 0)
                levels.Add(targetLevel);
        }

        private static bool FramePathContains(IList<SelectorLevel> levels, IEWindowController.IEDomElement frameElement)
        {
            var frameName = ReadAttribute(frameElement, "name");
            var frameId = ReadAttribute(frameElement, "id");
            foreach (var level in levels)
            {
                if (!string.Equals(level.TagName, "frm", StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var property in level.Properties.Where(p => p.IsSelected))
                {
                    if (string.Equals(property.Name, "name", StringComparison.OrdinalIgnoreCase)
                        && (string.Equals(property.Value, frameName, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(property.Value, frameId, StringComparison.OrdinalIgnoreCase)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsSelectorPathNode(IEWindowController.IEDomElement element)
        {
            if (!string.IsNullOrWhiteSpace(GetElementTag(element)))
                return true;

            try
            {
                var nodeType = element.get_attribute("nodeType");
                if (nodeType != null && int.TryParse(nodeType.ToString(), out var type))
                    return type == 1;
            }
            catch
            {
            }

            return false;
        }

        private static void PopulateElementProperties(
            SelectorLevel level,
            IEWindowController.IEDomElement element,
            IEWindowController controller,
            IList<object> framePath,
            IEWindowController.IEDomElement scope,
            bool preferUnique)
        {
            var tag = GetElementTag(element);
            if (string.IsNullOrWhiteSpace(tag))
                return;

            AddProperty(level, "tag", tag, true, true);
            AddProperty(level, "id", ReadAttribute(element, "id"), preferUnique, true);
            AddProperty(level, "name", ReadAttribute(element, "name"), preferUnique, true);
            AddProperty(level, "type", ReadAttribute(element, "type"), false, true);
            AddProperty(level, "class", ReadAttribute(element, "className"), false, true);
            AddProperty(level, "text", element.get_text(), false, true);
            AddProperty(level, "value", ReadAttribute(element, "value"), false, true);

            var index = GetIndexAmongMatches(controller, element, framePath, scope, level);
            if (index < 0)
                index = GetIndexAmongSameTagSiblings(element);
            if (index >= 0)
                AddProperty(level, "index", index.ToString(), false, true);

            EnumNeededProperties(level, controller, framePath, scope);
        }

        private static void EnumNeededProperties(
            SelectorLevel level,
            IEWindowController controller,
            IList<object> framePath,
            IEWindowController.IEDomElement scope)
        {
            var available = level.Properties
                .Where(p => ElementPropertyPriority.Contains(p.Name))
                .OrderBy(p => Array.IndexOf(ElementPropertyPriority, p.Name))
                .ToList();

            if (available.Count == 0)
                return;

            for (var count = 1; count <= available.Count; count++)
            {
                foreach (var prop in available)
                    prop.IsSelected = false;

                for (var i = 0; i < count; i++)
                    available[i].IsSelected = true;

                if (CountMatches(controller, framePath, scope, level) == 1)
                    return;
            }

            foreach (var prop in available)
                prop.IsSelected = true;
        }

        private static int CountMatches(
            IEWindowController controller,
            IList<object> framePath,
            IEWindowController.IEDomElement scope,
            SelectorLevel level)
        {
            try
            {
                return SelectorResolver.FindInScope(controller, framePath, scope, level, int.MaxValue).Count;
            }
            catch
            {
                return int.MaxValue;
            }
        }

        private static IEWindowController.IEDomElement ResolveScopeElement(
            IEWindowController controller,
            IList<object> framePath,
            IEWindowController.IEDomElement scope,
            SelectorLevel level)
        {
            try
            {
                var matches = SelectorResolver.FindInScope(controller, framePath, scope, level, 2);
                return matches.Count == 1 ? matches[0] : null;
            }
            catch
            {
                return null;
            }
        }

        private static int GetIndexAmongMatches(
            IEWindowController controller,
            IEWindowController.IEDomElement element,
            IList<object> framePath,
            IEWindowController.IEDomElement scope,
            SelectorLevel draftLevel)
        {
            try
            {
                var matches = SelectorResolver.FindInScope(controller, framePath, scope, draftLevel, int.MaxValue);
                for (var i = 0; i < matches.Count; i++)
                {
                    if (ReferenceEquals(matches[i].raw, element.raw))
                        return i;
                }
            }
            catch
            {
            }

            return -1;
        }

        private static int GetIndexAmongSameTagSiblings(IEWindowController.IEDomElement element)
        {
            try
            {
                var parent = element.get_parent();
                if (parent == null)
                    return -1;

                var tag = GetElementTag(element);
                if (string.IsNullOrWhiteSpace(tag))
                    return -1;

                var siblings = parent.get_children()
                    .Where(child => string.Equals(GetElementTag(child), tag, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                for (var i = 0; i < siblings.Count; i++)
                {
                    if (ReferenceEquals(siblings[i].raw, element.raw))
                        return i;
                }
            }
            catch
            {
            }

            return -1;
        }

        private static int GetFrameIndexAmongSiblings(IEWindowController.IEDomElement frameElement)
        {
            try
            {
                var parent = frameElement.get_parent();
                if (parent == null)
                    return 0;

                var frames = parent.get_children()
                    .Where(child => IsFrameTag(GetElementTag(child)))
                    .ToList();

                for (var i = 0; i < frames.Count; i++)
                {
                    if (ReferenceEquals(frames[i].raw, frameElement.raw))
                        return i;
                }
            }
            catch
            {
            }

            return 0;
        }

        private static bool IsFrameTag(string tag)
        {
            return string.Equals(tag, "iframe", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tag, "frame", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetElementTag(IEWindowController.IEDomElement element)
        {
            if (element == null)
                return string.Empty;

            try
            {
                var tag = element.get_tag_name();
                if (!string.IsNullOrWhiteSpace(tag))
                    return tag.Trim();
            }
            catch
            {
            }

            var tagName = ReadAttribute(element, "tagName");
            if (!string.IsNullOrWhiteSpace(tagName))
                return tagName.Trim();

            var nodeName = ReadAttribute(element, "nodeName");
            if (!string.IsNullOrWhiteSpace(nodeName))
                return nodeName.Trim().TrimStart('#');

            if (!string.IsNullOrWhiteSpace(ReadAttribute(element, "type")))
                return "input";

            return string.Empty;
        }

        private static string ReadAttribute(IEWindowController.IEDomElement element, string name)
        {
            try
            {
                return element.get_attribute(name)?.ToString();
            }
            catch
            {
                return null;
            }
        }

        private static void AddProperty(SelectorLevel level, string name, string value, bool selected, bool canToggle)
        {
            if (string.IsNullOrEmpty(value))
                return;

            level.Properties.Add(new ElementPropertyItem
            {
                Name = name,
                Value = value,
                IsSelected = selected,
                CanToggle = canToggle
            });
        }
    }
}
