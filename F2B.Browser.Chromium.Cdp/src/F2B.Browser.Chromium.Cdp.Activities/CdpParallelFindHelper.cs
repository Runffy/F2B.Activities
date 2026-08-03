using System;
using System.Collections.Generic;
using System.Threading;
using F2B.Browser.Chromium.Cdp;
using F2B.Browser.Chromium.Cdp.Browser;
using F2B.Browser.Chromium.Cdp.Selectors;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    internal static class CdpParallelFindHelper
    {
        public static CdpParallelFindElementResult ParallelFind(
            CdpTab tab,
            CdpElement elementRoot,
            IList<string> selectors,
            int timeoutMs)
        {
            if (selectors == null || selectors.Count == 0)
            {
                return CdpParallelFindElementResult.NotFound();
            }

            if (elementRoot != null)
            {
                return ParallelFindFromElement(elementRoot, selectors, timeoutMs);
            }

            return ParallelFindFromTabContext(tab, selectors, timeoutMs);
        }

        private static CdpParallelFindElementResult ParallelFindFromElement(
            CdpElement elementRoot,
            IList<string> selectors,
            int timeoutMs)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(0, timeoutMs));
            do
            {
                for (var i = 0; i < selectors.Count; i++)
                {
                    var selector = selectors[i];
                    if (string.IsNullOrWhiteSpace(selector))
                    {
                        continue;
                    }

                    if (SelectorXmlSerializer.HasWndLevel(selector))
                    {
                        throw new InvalidOperationException(
                            "Selectors must not contain <wnd> when Input Element is provided.");
                    }

                    var found = elementRoot.FindElement(selector, 0, false);
                    if (found != null)
                    {
                        return CdpParallelFindElementResult.Create(i, found);
                    }
                }

                if (timeoutMs <= 0)
                {
                    break;
                }

                Thread.Sleep(10);
            }
            while (DateTime.UtcNow < deadline);

            return CdpParallelFindElementResult.NotFound();
        }

        private static CdpParallelFindElementResult ParallelFindFromTabContext(
            CdpTab tab,
            IList<string> selectors,
            int timeoutMs)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(0, timeoutMs));
            do
            {
                for (var i = 0; i < selectors.Count; i++)
                {
                    var selector = selectors[i];
                    if (string.IsNullOrWhiteSpace(selector))
                    {
                        continue;
                    }

                    CdpElement found;
                    if (SelectorXmlSerializer.HasWndLevel(selector))
                    {
                        found = TryFindWithWndSelector(selector, tab);
                    }
                    else
                    {
                        if (tab == null)
                        {
                            throw new InvalidOperationException(
                                "Input Tab must be provided when selectors do not contain <wnd>.");
                        }

                        found = tab.FindElement(selector, 0, false);
                    }

                    if (found != null)
                    {
                        return CdpParallelFindElementResult.Create(i, found);
                    }
                }

                if (timeoutMs <= 0)
                {
                    break;
                }

                Thread.Sleep(10);
            }
            while (DateTime.UtcNow < deadline);

            return CdpParallelFindElementResult.NotFound();
        }

        private static CdpElement TryFindWithWndSelector(string selector, CdpTab inputTab)
        {
            try
            {
                var resolvedTab = inputTab ?? CdpTabFinder.FindTab(selector).Tab;
                var operationXml = SelectorXmlSerializer.ToOperationXml(SelectorXmlSerializer.SplitScope(selector));
                if (string.IsNullOrWhiteSpace(operationXml))
                {
                    return null;
                }

                return resolvedTab.FindElement(operationXml, 0, false);
            }
            catch
            {
                return null;
            }
        }
    }
}
