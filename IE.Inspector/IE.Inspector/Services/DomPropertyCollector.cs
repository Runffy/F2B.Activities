using System.Collections.Generic;
using F2B.Browser.IExplore.COM;
using IE.Inspector.Models;

namespace IE.Inspector.Services
{
    public static class DomPropertyCollector
    {
        private static readonly string[] StandardAttributes =
        {
            "tagName", "id", "name", "className", "type", "value", "href", "src", "title", "alt", "role", "innerText"
        };

        public static IList<ElementPropertyItem> CollectAll(IEWindowController.IEDomElement element, IEWindowController controller)
        {
            var items = new List<ElementPropertyItem>();
            if (element == null)
                return items;

            foreach (var attribute in StandardAttributes)
            {
                var value = ReadValue(element, attribute);
                TryAdd(items, NormalizeName(attribute), value);
            }

            if (controller != null)
            {
                TryAdd(items, "url", controller.url);
                TryAdd(items, "window_title", controller.title);
                if (controller.hwnd.HasValue)
                    TryAdd(items, "hwnd", "0x" + controller.hwnd.Value.ToString("X"));
                if (controller.doc_hwnd.HasValue)
                    TryAdd(items, "doc_hwnd", "0x" + controller.doc_hwnd.Value.ToString("X"));
            }

            var bounds = controller?.get_element_screen_bounds(element, element.build_frame_path());
            if (bounds.HasValue && !bounds.Value.IsEmpty)
            {
                TryAdd(items, "BoundingRectangle", bounds.Value.ToString());
                TryAdd(items, "X", bounds.Value.X.ToString());
                TryAdd(items, "Y", bounds.Value.Y.ToString());
                TryAdd(items, "Width", bounds.Value.Width.ToString());
                TryAdd(items, "Height", bounds.Value.Height.ToString());
            }

            return items;
        }

        private static string ReadValue(IEWindowController.IEDomElement element, string attribute)
        {
            try
            {
                if (attribute == "innerText")
                    return element.get_text();

                return element.get_attribute(attribute)?.ToString();
            }
            catch
            {
                return null;
            }
        }

        private static string NormalizeName(string attribute)
        {
            if (string.Equals(attribute, "tagName", System.StringComparison.OrdinalIgnoreCase))
                return "tag";
            if (string.Equals(attribute, "className", System.StringComparison.OrdinalIgnoreCase))
                return "class";

            return attribute;
        }

        private static void TryAdd(IList<ElementPropertyItem> items, string name, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            items.Add(new ElementPropertyItem
            {
                Name = name,
                Value = value,
                CanToggle = false
            });
        }
    }
}
