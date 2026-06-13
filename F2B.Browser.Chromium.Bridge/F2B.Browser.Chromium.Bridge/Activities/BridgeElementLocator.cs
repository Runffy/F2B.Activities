using F2B.Browser.Chromium.Bridge.Selectors;

namespace F2B.Browser.Chromium.Bridge
{
    internal static class BridgeElementLocator
    {
        public static BwElement FindBySelector(
            string selectorXml,
            BwTab inputTab,
            int index,
            int timeoutMs,
            int delayBefore)
        {
            if (SelectorXmlSerializer.HasWndLevel(selectorXml))
            {
                var executionContext = BridgeActivityServices.Host.ResolveContext(selectorXml, inputTab);
                return BridgeActivityServices.Host
                    .GetClient(executionContext.InstanceId)
                    .FindElement(executionContext, index, timeoutMs, BridgeFindElementWaitState.Attached, delayBefore);
            }

            BridgeSelectorRules.EnsureTabOrWnd(selectorXml, inputTab);
            return inputTab.FindElement(
                selectorXml,
                index,
                timeoutMs,
                BridgeFindElementWaitState.Attached,
                delayBefore);
        }

        public static bool Exists(string selectorXml, BwTab inputTab, int index)
        {
            if (SelectorXmlSerializer.HasWndLevel(selectorXml))
            {
                var executionContext = BridgeActivityServices.Host.ResolveContext(selectorXml, inputTab);
                return BridgeActivityServices.Host
                    .GetClient(executionContext.InstanceId)
                    .ElementExists(executionContext, index);
            }

            BridgeSelectorRules.EnsureTabOrWnd(selectorXml, inputTab);
            return inputTab.ElementExists(selectorXml, index);
        }

        public static BwElement[] FindAllBySelector(string selectorXml, BwTab inputTab)
        {
            if (SelectorXmlSerializer.HasWndLevel(selectorXml))
            {
                var executionContext = BridgeActivityServices.Host.ResolveContext(selectorXml, inputTab);
                return BridgeActivityServices.Host
                    .GetClient(executionContext.InstanceId)
                    .FindElements(executionContext);
            }

            BridgeSelectorRules.EnsureTabOrWnd(selectorXml, inputTab);
            return inputTab.FindElements(selectorXml);
        }
    }
}
