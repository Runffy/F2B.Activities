using System;
using System.Collections.Generic;
using System.Linq;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Inspector.Models;

namespace FlaUI.Inspector.Services
{
    public static class SelectorBuilder
    {
        private static readonly string[] PropertyPriority = { "ControlType", "AutomationId", "Name", "ClassName", "FrameworkId" };
        private static readonly string[] RootWindowPropertyPriority = { "ControlType", "Title", "ClassName", "AutomationId" };

        public static IList<AutomationElement> BuildPathToRoot(AutomationElement target)
        {
            return ElementPathHelper.BuildPathFromApplicationRoot(target);
        }

        public static IList<SelectorLevel> BuildLevels(AutomationElement target)
        {
            var path = BuildPathToRoot(target);
            var levels = new List<SelectorLevel>();

            for (var i = 0; i < path.Count; i++)
            {
                var element = path[i];
                var level = new SelectorLevel(element);
                PopulateSelectorProperties(level, element, i == 0, i == path.Count - 1);
                ConfigureDefaultEnabled(level, i, path.Count);
                levels.Add(level);
            }

            return levels;
        }

        private static void PopulateSelectorProperties(SelectorLevel level, AutomationElement element, bool isRoot, bool isTarget)
        {
            if (isRoot)
            {
                AddSelectorProperty(level, "ControlType", "Window", true, false);
                AddSelectorProperty(level, "Title", GetWindowTitle(element), true, true);
                AddSelectorProperty(level, "ClassName", GetClassName(element), true, true);
                AddSelectorProperty(level, "AutomationId", GetAutomationId(element), false, true);

                try
                {
                    var processId = element.Properties.ProcessId.ValueOrDefault;
                    if (processId > 0)
                    {
                        var process = System.Diagnostics.Process.GetProcessById(processId);
                        AddSelectorProperty(level, "ProcessName", process.ProcessName, true, true);
                        try
                        {
                            AddSelectorProperty(level, "FileName", process.MainModule?.FileName, false, true);
                        }
                        catch
                        {
                        }
                    }
                }
                catch
                {
                }

                EnumNeededProperties(level, element, isRootWindow: true);
                TryMinimizeProcessName(level, element);
                TryEnsureTitleSelected(level, element);
                return;
            }

            AddSelectorProperty(level, "ControlType", GetControlType(element), true, true);
            AddSelectorProperty(level, "AutomationId", GetAutomationId(element), false, true);
            AddSelectorProperty(level, "Name", GetName(element), false, true);
            AddSelectorProperty(level, "ClassName", GetClassName(element), false, true);
            AddSelectorProperty(level, "FrameworkId", GetFrameworkId(element), false, true);

            var indexInParent = GetIndexInParent(element);
            if (indexInParent >= 0)
                AddSelectorProperty(level, "IndexInParent", indexInParent.ToString(), false, true);

            EnumNeededProperties(level, element, isRootWindow: false);
        }

        private static void ConfigureDefaultEnabled(SelectorLevel level, int index, int totalCount)
        {
            if (index == 0 || index == totalCount - 1)
            {
                level.IsEnabled = true;
                level.CanDisable = false;
                return;
            }

            var className = level.Properties.FirstOrDefault(p => p.Name == "ClassName")?.Value ?? string.Empty;
            if (className.Equals("ShellDll_DefView", StringComparison.OrdinalIgnoreCase))
            {
                level.IsEnabled = false;
                level.CanDisable = true;
                return;
            }

            level.IsEnabled = true;
            level.CanDisable = true;
        }

        private static void EnumNeededProperties(SelectorLevel level, AutomationElement element, bool isRootWindow)
        {
            AutomationElement parent;
            try
            {
                parent = element.Parent;
            }
            catch
            {
                return;
            }

            if (parent == null)
                return;

            var priority = isRootWindow ? RootWindowPropertyPriority : PropertyPriority;
            var available = level.Properties
                .Where(p => priority.Contains(p.Name))
                .OrderBy(p => Array.IndexOf(priority, p.Name))
                .ToList();

            if (available.Count == 0)
                return;

            for (var count = 1; count <= available.Count; count++)
            {
                foreach (var prop in available)
                    prop.IsSelected = false;

                for (var i = 0; i < count; i++)
                    available[i].IsSelected = true;

                var matches = CountMatches(parent, level);
                if (matches == 1)
                    return;
            }

            foreach (var prop in available)
                prop.IsSelected = true;
        }

        private static int CountMatches(AutomationElement parent, SelectorLevel level)
        {
            try
            {
                return SelectorResolver.FindMatchingElements(parent, level, searchDescendants: false).Count;
            }
            catch
            {
                return int.MaxValue;
            }
        }

        private static void TryMinimizeProcessName(SelectorLevel level, AutomationElement element)
        {
            var processProperty = level.Properties.FirstOrDefault(p => p.Name == "ProcessName");
            if (processProperty == null || !processProperty.IsSelected)
                return;

            AutomationElement parent;
            try
            {
                parent = element.Parent;
            }
            catch
            {
                return;
            }

            if (parent == null)
                return;

            processProperty.IsSelected = false;
            if (CountMatches(parent, level) != 1)
                processProperty.IsSelected = true;
        }

        private static void TryEnsureTitleSelected(SelectorLevel level, AutomationElement element)
        {
            var titleProperty = level.Properties.FirstOrDefault(p => p.Name == "Title");
            if (titleProperty == null || titleProperty.IsSelected || string.IsNullOrEmpty(titleProperty.Value))
                return;

            AutomationElement parent;
            try
            {
                parent = element.Parent;
            }
            catch
            {
                return;
            }

            if (parent == null)
                return;

            if (CountMatches(parent, level) != 1)
                titleProperty.IsSelected = true;
        }

        public static ConditionBase[] BuildConditions(FlaUI.Core.AutomationBase automation, IEnumerable<ElementPropertyItem> properties)
        {
            var conditions = new List<ConditionBase>();
            var factory = automation.ConditionFactory;

            foreach (var property in properties.Where(p => p.IsSelected && !string.IsNullOrEmpty(p.Value) && !p.IsRegex))
            {
                switch (property.Name)
                {
                    case "ControlType":
                        if (Enum.TryParse(property.Value, out ControlType controlType))
                            conditions.Add(factory.ByControlType(controlType));
                        break;
                    case "Name":
                    case "Title":
                        conditions.Add(factory.ByName(property.Value));
                        break;
                    case "AutomationId":
                        conditions.Add(factory.ByAutomationId(property.Value));
                        break;
                    case "ClassName":
                        conditions.Add(factory.ByClassName(property.Value));
                        break;
                    case "FrameworkId":
                        conditions.Add(new PropertyCondition(automation.PropertyLibrary.Element.FrameworkId, property.Value));
                        break;
                }
            }

            return conditions.ToArray();
        }

        private static void AddSelectorProperty(SelectorLevel level, string name, string value, bool selected, bool canToggle)
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

        private static int GetIndexInParent(AutomationElement element)
        {
            try
            {
                var parent = element.Parent;
                if (parent == null)
                    return -1;

                var children = parent.FindAllChildren();
                for (var i = 0; i < children.Length; i++)
                {
                    if (element.Equals(children[i]))
                        return i;
                }
            }
            catch
            {
            }

            return -1;
        }

        private static string GetControlType(AutomationElement element)
        {
            try
            {
                if (element.Properties.ControlType.IsSupported)
                {
                    var value = element.Properties.ControlType.Value;
                    if (value != ControlType.Unknown)
                        return value.ToString();
                }
            }
            catch
            {
            }

            return null;
        }

        private static string GetWindowTitle(AutomationElement element)
        {
            return GetName(element);
        }

        private static string GetName(AutomationElement element)
        {
            try
            {
                if (element.Properties.Name.IsSupported)
                    return element.Properties.Name.ValueOrDefault;
            }
            catch { }
            return null;
        }

        private static string GetAutomationId(AutomationElement element)
        {
            try
            {
                if (element.Properties.AutomationId.IsSupported)
                    return element.Properties.AutomationId.ValueOrDefault;
            }
            catch { }
            return null;
        }

        private static string GetClassName(AutomationElement element)
        {
            try
            {
                if (element.Properties.ClassName.IsSupported)
                    return element.Properties.ClassName.ValueOrDefault;
            }
            catch { }
            return null;
        }

        private static string GetFrameworkId(AutomationElement element)
        {
            try
            {
                if (element.Properties.FrameworkId.IsSupported)
                    return element.Properties.FrameworkId.ValueOrDefault;
            }
            catch { }
            return null;
        }
    }
}
