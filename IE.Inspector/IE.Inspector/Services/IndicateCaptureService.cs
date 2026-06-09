using System.Collections.Generic;
using System.Linq;
using F2B.Browser.IExplore.COM;
using IE.Inspector.Models;

namespace IE.Inspector.Services
{
    public static class IndicateCaptureService
    {
        public static IndicateCaptureResult Capture(
            IEWindowController controller,
            IEWindowController.IEDomElement element,
            IList<object> framePath)
        {
            if (controller == null || element == null)
                return null;

            framePath = framePath ?? element.build_frame_path() ?? new List<object>();
            var levels = CloneLevels(SelectorBuilder.BuildLevels(controller, element, framePath));
            var treeRoot = DomTreeBuilder.BuildDocumentTree(element);

            return new IndicateCaptureResult
            {
                Controller = controller,
                TargetElement = element,
                FramePath = framePath,
                SelectorLevels = levels,
                SelectorXmlSnapshot = SelectorXmlSerializer.Serialize(levels),
                PathDepth = DomTreeBuilder.BuildPathToDocumentRoot(element).Count,
                PropertyItems = DomPropertyCollector.CollectAll(element, controller),
                DomTreeRoot = treeRoot,
                SelectedDomNode = treeRoot != null
                    ? DomTreeBuilder.FindNode(treeRoot, element) ?? treeRoot
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

        private static string BuildTargetDisplay(IEWindowController.IEDomElement element)
        {
            try
            {
                var tag = element.get_attribute("tagName")?.ToString() ?? "element";
                var id = element.get_attribute("id")?.ToString();
                var name = element.get_attribute("name")?.ToString();
                var label = !string.IsNullOrEmpty(id) ? id : name;
                return string.IsNullOrEmpty(label) ? tag : tag + " '" + label + "'";
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}
