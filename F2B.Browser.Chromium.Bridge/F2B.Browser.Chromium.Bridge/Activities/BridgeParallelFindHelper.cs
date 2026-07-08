using System;
using System.Linq;
using System.Threading;
using F2B.Browser.Chromium.Bridge.Selectors;

namespace F2B.Browser.Chromium.Bridge
{
    internal static class BridgeParallelFindHelper
    {
        public static int Find(
            BwTab tab,
            BwElement element,
            string[] selectors,
            int timeoutMs,
            BridgeFindElementWaitState waitState)
        {
            if (selectors == null || selectors.Length == 0)
            {
                throw new InvalidOperationException("Selectors is required and cannot be empty.");
            }

            if (tab != null)
            {
                EnsureNoWndInSelectors(selectors, "ParentObject is BwTab");
                return tab.ParallelFindElement(selectors, timeoutMs, waitState);
            }

            if (element != null)
            {
                EnsureNoWndInSelectors(selectors, "ParentObject is BwElement");
                return element.ParallelFindElement(selectors, timeoutMs, waitState);
            }

            EnsureWndInEverySelector(selectors);
            return FindFromWndSelectors(selectors, timeoutMs, waitState);
        }

        private static void EnsureNoWndInSelectors(string[] selectors, string context)
        {
            for (var i = 0; i < selectors.Length; i++)
            {
                var selector = selectors[i];
                if (string.IsNullOrWhiteSpace(selector))
                {
                    continue;
                }

                if (SelectorXmlSerializer.HasWndLevel(selector))
                {
                    throw new InvalidOperationException(
                        "Selectors must not contain <wnd> when " + context + ".");
                }
            }
        }

        private static void EnsureWndInEverySelector(string[] selectors)
        {
            for (var i = 0; i < selectors.Length; i++)
            {
                var selector = selectors[i];
                if (string.IsNullOrWhiteSpace(selector))
                {
                    continue;
                }

                if (!SelectorXmlSerializer.HasWndLevel(selector))
                {
                    throw new InvalidOperationException(
                        "Each selector must contain <wnd> when ParentObject is omitted.");
                }
            }
        }

        private static int FindFromWndSelectors(
            string[] selectors,
            int timeoutMs,
            BridgeFindElementWaitState waitState)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(0, timeoutMs));
            do
            {
                for (var i = 0; i < selectors.Length; i++)
                {
                    var selector = selectors[i];
                    if (string.IsNullOrWhiteSpace(selector))
                    {
                        continue;
                    }

                    if (TryMatchWndSelector(selector, waitState))
                    {
                        return i;
                    }
                }

                if (timeoutMs <= 0)
                {
                    break;
                }

                Thread.Sleep(100);
            }
            while (DateTime.UtcNow < deadline);

            return -1;
        }

        private static bool TryMatchWndSelector(string selector, BridgeFindElementWaitState waitState)
        {
            try
            {
                var scope = SelectorXmlSerializer.SplitScope(selector);
                var operationXml = SelectorXmlSerializer.ToOperationXml(scope);
                if (string.IsNullOrWhiteSpace(operationXml))
                {
                    return false;
                }

                var remainingMs = 0;
                var context = BridgeActivityServices.Host.ResolveContext(selector, null, remainingMs);
                var matchedIndex = context.Tab.ParallelFindElement(
                    new[] { operationXml },
                    0,
                    waitState);

                return matchedIndex >= 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
