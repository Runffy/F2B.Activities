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
                var tab = inputTab ?? CdpTabFinder.FindTab(selectorXml).Tab;
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
                var tab = inputTab ?? CdpTabFinder.FindTab(selectorXml).Tab;
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
                return parentElement.FindElements(selectorXml);
            }

            if (SelectorXmlSerializer.HasWndLevel(selectorXml))
            {
                var tab = inputTab ?? CdpTabFinder.FindTab(selectorXml).Tab;
                var operationXml = SelectorXmlSerializer.ToOperationXml(SelectorXmlSerializer.SplitScope(selectorXml));
                return tab.FindElements(operationXml);
            }

            CdpSelectorRules.EnsureTabOrWnd(selectorXml, inputTab);
            return inputTab.FindElements(selectorXml);
        }
    }
}
