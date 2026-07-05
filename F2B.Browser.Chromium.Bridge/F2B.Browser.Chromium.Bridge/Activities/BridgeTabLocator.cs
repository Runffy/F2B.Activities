using System;
using F2B.Browser.Chromium.Bridge.Selectors;

namespace F2B.Browser.Chromium.Bridge
{
    internal static class BridgeTabLocator
    {
        public static BwTab Resolve(string selectorXml, BwTab tab, int timeoutMs = 0)
        {
            if (!string.IsNullOrWhiteSpace(selectorXml) && SelectorXmlSerializer.HasWndLevel(selectorXml))
            {
                BridgeTabResolver.EnsureWndOnlySelector(selectorXml);
                return BridgeActivityServices.Host.ResolveContext(selectorXml, tab, timeoutMs).Tab;
            }

            if (tab == null)
            {
                throw new InvalidOperationException(
                    "Either Input Tab or a <wnd> Selector must be provided.");
            }

            return tab;
        }
    }
}
