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
                var deadline = System.DateTime.UtcNow.AddMilliseconds(timeoutMs);
                var executionContext = BridgeActivityServices.Host.ResolveContext(selectorXml, inputTab, timeoutMs);
                var remainingMs = RemainingMs(deadline);
                if (remainingMs <= 0)
                {
                    throw new System.TimeoutException(
                        "Timeout expired while resolving <wnd> selector before FindElement.");
                }

                return BridgeActivityServices.Host
                    .GetClient(executionContext.InstanceId)
                    .FindElement(
                        executionContext,
                        index,
                        remainingMs,
                        BridgeFindElementWaitState.Attached,
                        delayBefore);
            }

            BridgeSelectorRules.EnsureTabOrWnd(selectorXml, inputTab);
            return inputTab.FindElement(
                selectorXml,
                index,
                timeoutMs,
                BridgeFindElementWaitState.Attached,
                delayBefore);
        }

        public static bool Exists(string selectorXml, BwTab inputTab, int index, int timeoutMs = 15000)
        {
            if (SelectorXmlSerializer.HasWndLevel(selectorXml))
            {
                var executionContext = BridgeActivityServices.Host.ResolveContext(selectorXml, inputTab, timeoutMs);
                return BridgeActivityServices.Host
                    .GetClient(executionContext.InstanceId)
                    .ElementExists(executionContext, index);
            }

            BridgeSelectorRules.EnsureTabOrWnd(selectorXml, inputTab);
            return inputTab.ElementExists(selectorXml, index);
        }

        public static BwElement[] FindAllBySelector(string selectorXml, BwTab inputTab, int timeoutMs = 15000)
        {
            if (SelectorXmlSerializer.HasWndLevel(selectorXml))
            {
                var executionContext = BridgeActivityServices.Host.ResolveContext(selectorXml, inputTab, timeoutMs);
                return BridgeActivityServices.Host
                    .GetClient(executionContext.InstanceId)
                    .FindElements(executionContext);
            }

            BridgeSelectorRules.EnsureTabOrWnd(selectorXml, inputTab);
            return inputTab.FindElements(selectorXml);
        }

        private static int RemainingMs(System.DateTime deadline)
        {
            return System.Math.Max(0, (int)(deadline - System.DateTime.UtcNow).TotalMilliseconds);
        }
    }
}
