using System;
using System.Collections.Generic;
using System.Linq;

namespace F2B.DesktopApplication.FlaUI.Selectors
{
    public static class SelectorScopeHelper
    {
        public static IList<SelectorLevel> Parse(string selectorXml)
        {
            return SelectorXmlSerializer.Deserialize(selectorXml);
        }

        public static void EnsureWindowOnly(IList<SelectorLevel> levels)
        {
            if (levels == null || levels.Count == 0)
                throw new ArgumentException("Window selector must contain at least one <wnd> tag.");

            if (levels.Any(level => !string.Equals(level.TagName, "wnd", StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Window selector accepts <wnd> tags only.");
        }

        public static void EnsureControlOnly(IList<SelectorLevel> levels)
        {
            if (levels == null || levels.Count == 0)
                throw new ArgumentException("Control selector must contain at least one <ctrl> tag.");

            if (levels.Any(level => !string.Equals(level.TagName, "ctrl", StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("When Input Window is provided, selector must contain <ctrl> tags only.");
        }
    }
}
