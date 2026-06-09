using System;
using System.Collections.Generic;
using System.Linq;
using F2B.Browser.IExplore.COM;
using IE.Inspector.Models;

namespace IE.Inspector.Services
{
    public sealed class SelectorResolver
    {
        private readonly IeBrowserContext _context;

        public SelectorResolver(IeBrowserContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public IList<IEWindowController.IEDomElement> FindElements(
            IEnumerable<SelectorLevel> levels,
            int maxResults = 2)
        {
            var enabledLevels = levels?.Where(l => l.IsEnabled).ToList() ?? new List<SelectorLevel>();
            var controller = EnsureController(enabledLevels);
            if (controller == null)
                return new List<IEWindowController.IEDomElement>();

            var framePath = SelectorLocatorBuilder.BuildFramePathFromLevels(enabledLevels);
            var ctrlLevels = enabledLevels
                .Where(l => string.Equals(l.TagName, "ctrl", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (ctrlLevels.Count == 0)
                return new List<IEWindowController.IEDomElement>();

            IEWindowController.IEDomElement scope = null;
            for (var i = 0; i < ctrlLevels.Count - 1; i++)
            {
                var matches = FindInScope(controller, framePath, scope, ctrlLevels[i], maxResults: int.MaxValue);
                if (matches.Count != 1)
                    return matches.Take(maxResults).ToList();

                scope = matches[0];
            }

            return FindInScope(controller, framePath, scope, ctrlLevels[ctrlLevels.Count - 1], maxResults);
        }

        public static IList<IEWindowController.IEDomElement> FindInScope(
            IEWindowController controller,
            IList<object> framePath,
            IEWindowController.IEDomElement scope,
            SelectorLevel level,
            int maxResults)
        {
            if (controller == null || level == null)
                return new List<IEWindowController.IEDomElement>();

            var locator = SelectorLocatorBuilder.BuildLocatorFromLevel(level);
            if (locator == null || locator.Count == 0)
                return new List<IEWindowController.IEDomElement>();

            var matches = controller.find_elements(locator, framePath) ?? new IEWindowController.IEDomElement[0];
            IEnumerable<IEWindowController.IEDomElement> scoped = matches;
            if (scope != null)
                scoped = matches.Where(match => IsDescendantOf(match, scope));

            return scoped.Take(maxResults).ToList();
        }

        private static bool IsDescendantOf(IEWindowController.IEDomElement element, IEWindowController.IEDomElement ancestor)
        {
            if (element == null || ancestor == null)
                return false;

            var current = element;
            while (current != null)
            {
                if (ReferenceEquals(current.raw, ancestor.raw))
                    return true;

                current = current.get_parent();
            }

            return false;
        }

        private IEWindowController EnsureController(IList<SelectorLevel> levels)
        {
            if (_context.Controller != null)
                return _context.Controller;

            var windowLevel = levels.FirstOrDefault(l => string.Equals(l.TagName, "wnd", StringComparison.OrdinalIgnoreCase));
            if (windowLevel == null)
                return null;

            var hwndProperty = windowLevel.Properties.FirstOrDefault(p => p.IsSelected && string.Equals(p.Name, "hwnd", StringComparison.OrdinalIgnoreCase));
            if (hwndProperty != null && TryParseHwnd(hwndProperty.Value, out var hwnd))
            {
                _context.Controller = IEWindowController.connect_embedded_ie_window(hwnd: hwnd, timeout: 5000);
                return _context.Controller;
            }

            var titleProperty = windowLevel.Properties.FirstOrDefault(p => p.IsSelected && string.Equals(p.Name, "title", StringComparison.OrdinalIgnoreCase));
            _context.Controller = IEWindowController.connect_embedded_ie_window(title: titleProperty?.Value, timeout: 5000);
            return _context.Controller;
        }

        private static bool TryParseHwnd(string value, out long hwnd)
        {
            hwnd = 0;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var text = value.Trim();
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return long.TryParse(text.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out hwnd);

            return long.TryParse(text, out hwnd);
        }
    }
}
