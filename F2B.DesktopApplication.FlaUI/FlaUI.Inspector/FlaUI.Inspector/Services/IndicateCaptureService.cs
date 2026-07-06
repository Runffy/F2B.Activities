using System.Collections.Generic;
using System.Linq;
using FlaUI.Core.AutomationElements;
using FlaUI.Inspector.Models;

namespace FlaUI.Inspector.Services
{
    public static class IndicateCaptureService
    {
        public static IndicateCaptureResult Capture(AutomationElement element)
        {
            if (element == null)
                return null;

            var path = SelectorBuilder.BuildPathToRoot(element);
            var levels = CloneLevels(SelectorBuilder.BuildLevels(element));
            var treeRoot = VisualTreeBuilder.BuildWindowTree(element);

            return new IndicateCaptureResult
            {
                TargetElement = element,
                SelectorLevels = levels,
                SelectorXmlSnapshot = SelectorXmlSerializer.Serialize(levels),
                PathDepth = path.Count,
                PropertyItems = PropertyCollector.CollectAll(element),
                VisualTreeRoot = treeRoot,
                SelectedVisualNode = treeRoot != null
                    ? VisualTreeBuilder.FindNode(treeRoot, element) ?? treeRoot
                    : null,
                TargetDisplay = BuildTargetDisplay(element)
            };
        }

        public static IList<SelectorLevel> CloneLevels(IEnumerable<SelectorLevel> levels)
        {
            return levels?.Select(CloneLevel).ToList() ?? new List<SelectorLevel>();
        }

        private static SelectorLevel CloneLevel(SelectorLevel source)
        {
            var clone = new SelectorLevel(source.TagName)
            {
                IsEnabled = source.IsEnabled,
                CanDisable = source.CanDisable
            };

            foreach (var property in source.Properties)
            {
                clone.Properties.Add(new ElementPropertyItem
                {
                    Name = property.Name,
                    Value = property.Value,
                    IsRegex = property.IsRegex,
                    IsSelected = property.IsSelected,
                    CanToggle = property.CanToggle
                });
            }

            clone.RefreshTagLine();
            return clone;
        }

        private static string BuildTargetDisplay(AutomationElement element)
        {
            try
            {
                var role = element.Properties.ControlType.IsSupported
                    ? element.Properties.ControlType.Value.ToString()
                    : "element";
                var name = element.Properties.Name.IsSupported ? element.Properties.Name.ValueOrDefault : string.Empty;
                return string.IsNullOrEmpty(name) ? role : role + " '" + name + "'";
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}
