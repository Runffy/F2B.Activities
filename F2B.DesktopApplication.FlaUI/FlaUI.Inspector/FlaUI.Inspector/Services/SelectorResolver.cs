using System;
using System.Collections.Generic;
using System.Linq;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Inspector.Helpers;
using FlaUI.Inspector.Models;

namespace FlaUI.Inspector.Services
{
    public sealed class SelectorResolver
    {
        public IList<AutomationElement> FindElements(IEnumerable<SelectorLevel> levels, int maxResults = int.MaxValue)
        {
            var enabledLevels = levels?.Where(l => l.IsEnabled).ToList() ?? new List<SelectorLevel>();
            if (enabledLevels.Count == 0)
                return new List<AutomationElement>();

            var automation = AutomationContext.Instance.Automation;
            var current = new List<AutomationElement> { automation.GetDesktop() };

            for (var i = 0; i < enabledLevels.Count; i++)
            {
                var level = enabledLevels[i];
                var next = new List<AutomationElement>();
                var searchDescendants = i > 0;

                foreach (var parent in current)
                {
                    var matches = FindMatchingElements(parent, level, searchDescendants);
                    next.AddRange(matches);
                    if (next.Count >= maxResults)
                        break;
                }

                current = next.Distinct().Take(maxResults).ToList();
                if (current.Count == 0)
                    break;
            }

            return current;
        }

        public static IList<AutomationElement> FindMatchingElements(
            AutomationElement parent,
            SelectorLevel level,
            bool searchDescendants)
        {
            var automation = AutomationContext.Instance.Automation;
            var selectedProperties = level.Properties.Where(p => p.IsSelected).ToList();
            var processProperty = selectedProperties.FirstOrDefault(p => p.Name == "ProcessName" && !string.IsNullOrEmpty(p.Value));
            var indexInParentText = selectedProperties.FirstOrDefault(p => p.Name == "IndexInParent")?.Value;

            var conditions = SelectorBuilder.BuildConditions(automation, selectedProperties);
            AutomationElement[] candidates;

            try
            {
                if (searchDescendants)
                {
                    candidates = conditions.Length == 0
                        ? parent.FindAllDescendants()
                        : parent.FindAllDescendants(new AndCondition(conditions));
                }
                else if (conditions.Length == 0)
                {
                    candidates = parent.FindAllChildren();
                }
                else
                {
                    candidates = parent.FindAllChildren(new AndCondition(conditions));
                }
            }
            catch
            {
                return new List<AutomationElement>();
            }

            var filtered = candidates.Where(c => MatchLevel(c, level)).ToList();

            if (WindowVisibilityHelper.IsWindowLevel(level))
                filtered = filtered.Where(WindowVisibilityHelper.IsVisibleWindow).ToList();

            if (processProperty != null)
                filtered = filtered.Where(c => MatchProcessName(c, processProperty)).ToList();

            if (!searchDescendants && int.TryParse(indexInParentText, out var indexInParent) && indexInParent >= 0)
            {
                try
                {
                    var siblings = parent.FindAllChildren();
                    if (indexInParent < siblings.Length && MatchLevel(siblings[indexInParent], level))
                    {
                        var match = siblings[indexInParent];
                        if (!WindowVisibilityHelper.IsWindowLevel(level) || WindowVisibilityHelper.IsVisibleWindow(match))
                            return new List<AutomationElement> { match };
                    }
                }
                catch
                {
                }
            }

            return filtered;
        }

        public static bool MatchLevel(AutomationElement element, SelectorLevel level)
        {
            foreach (var property in level.Properties.Where(p => p.IsSelected && !string.IsNullOrEmpty(p.Value)))
            {
                if (!MatchProperty(element, property))
                    return false;
            }

            return true;
        }

        private static bool MatchProcessName(AutomationElement element, ElementPropertyItem property)
        {
            try
            {
                if (!element.Properties.ProcessId.IsSupported)
                    return false;

                var process = System.Diagnostics.Process.GetProcessById(element.Properties.ProcessId.ValueOrDefault);
                var processName = process.ProcessName ?? string.Empty;
                if (property.IsRegex)
                    return PropertyMatcher.IsRegexMatch(processName, property.Value);

                return PropertyMatcher.IsExactMatch(processName, property.Value, ignoreCase: true);
            }
            catch
            {
                return false;
            }
        }

        private static bool MatchProperty(AutomationElement element, ElementPropertyItem property)
        {
            try
            {
                switch (property.Name)
                {
                    case "ControlType":
                        if (!element.Properties.ControlType.IsSupported)
                            return false;
                        return PropertyMatcher.IsExactMatch(
                            element.Properties.ControlType.Value.ToString(),
                            property.Value,
                            ignoreCase: true);
                    case "Name":
                    case "Title":
                        if (!element.Properties.Name.IsSupported)
                            return false;
                        return MatchString(element.Properties.Name.ValueOrDefault, property);
                    case "AutomationId":
                        if (!element.Properties.AutomationId.IsSupported)
                            return false;
                        return MatchString(element.Properties.AutomationId.ValueOrDefault, property);
                    case "ClassName":
                        if (!element.Properties.ClassName.IsSupported)
                            return false;
                        return MatchString(element.Properties.ClassName.ValueOrDefault, property);
                    case "FrameworkId":
                        if (!element.Properties.FrameworkId.IsSupported)
                            return false;
                        return MatchString(element.Properties.FrameworkId.ValueOrDefault, property);
                    case "ProcessName":
                    case "FileName":
                    case "IndexInParent":
                        return true;
                }
            }
            catch
            {
                return false;
            }

            return true;
        }

        private static bool MatchString(string actual, ElementPropertyItem property)
        {
            if (property.IsRegex)
                return PropertyMatcher.IsRegexMatch(actual, property.Value);

            return PropertyMatcher.IsExactMatch(actual, property.Value);
        }
    }
}
