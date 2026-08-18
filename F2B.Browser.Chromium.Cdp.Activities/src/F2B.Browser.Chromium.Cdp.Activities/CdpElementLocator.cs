using System;
using F2B.Browser.Chromium.Cdp.Browser;
using F2B.Browser.Chromium.Cdp.Selectors;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    internal static class CdpElementLocator
    {
        public static CdpElement FindBySelector(
            string selectorXml,
            CdpTab inputTab,
            int index,
            int timeoutMs,
            int delayBefore,
            bool throwException = true)
        {
            if (SelectorXmlSerializer.HasWndLevel(selectorXml))
            {
                var tab = ResolveTab(selectorXml, inputTab, throwException);
                if (tab == null)
                {
                    return null;
                }

                CdpDelay.Apply(delayBefore);
                var operationXml = SelectorXmlSerializer.ToOperationXml(SelectorXmlSerializer.SplitScope(selectorXml));
                return tab.FindElement(operationXml, timeoutMs, throwException);
            }

            CdpSelectorRules.EnsureTabOrWnd(selectorXml, inputTab);
            CdpDelay.Apply(delayBefore);
            return inputTab.FindElement(selectorXml, timeoutMs, throwException);
        }

        public static bool Exists(string selectorXml, CdpTab inputTab)
        {
            if (SelectorXmlSerializer.HasWndLevel(selectorXml))
            {
                var tab = ResolveTab(selectorXml, inputTab, false);
                if (tab == null)
                {
                    return false;
                }

                var operationXml = SelectorXmlSerializer.ToOperationXml(SelectorXmlSerializer.SplitScope(selectorXml));
                return tab.ElementExists(operationXml);
            }

            CdpSelectorRules.EnsureTabOrWnd(selectorXml, inputTab);
            return inputTab.ElementExists(selectorXml);
        }

        public static CdpElement[] FindAllBySelector(string selectorXml, CdpTab inputTab, CdpElement parentElement)
        {
            if (parentElement != null)
            {
                return parentElement.FindElements(selectorXml) ?? new CdpElement[0];
            }

            if (SelectorXmlSerializer.HasWndLevel(selectorXml))
            {
                var tab = ResolveTab(selectorXml, inputTab, false);
                if (tab == null)
                {
                    return new CdpElement[0];
                }

                var operationXml = SelectorXmlSerializer.ToOperationXml(SelectorXmlSerializer.SplitScope(selectorXml));
                return tab.FindElements(operationXml) ?? new CdpElement[0];
            }

            CdpSelectorRules.EnsureTabOrWnd(selectorXml, inputTab);
            return inputTab.FindElements(selectorXml) ?? new CdpElement[0];
        }

        private static CdpTab ResolveTab(string selectorXml, CdpTab inputTab, bool throwException)
        {
            if (inputTab != null)
            {
                return inputTab;
            }

            var found = CdpTabFinder.FindTab(selectorXml, throwException);
            return found == null ? null : found.Tab;
        }
    }
}
