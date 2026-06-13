using System;
using F2B.Browser.Chromium.Bridge.Selectors;

namespace F2B.Browser.Chromium.Bridge
{
    internal static class BridgeSelectorRules
    {
        public static void EnsureTabOrWnd(string selectorXml, BwTab tab)
        {
            if (SelectorXmlSerializer.HasWndLevel(selectorXml))
                return;

            if (tab == null)
            {
                throw new InvalidOperationException(
                    "Selector has no <wnd> level. Input Tab must be provided, or include a <wnd> line in Selector XML.");
            }
        }
    }
}
