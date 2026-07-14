using System;
using System.Threading;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    internal static class CdpBrowserLocator
    {
        public static CdpBrowser Resolve(CdpBrowser browser, int? port)
        {
            if (browser != null)
            {
                return browser;
            }

            if (port.HasValue && port.Value > 0)
            {
                return CdpBrowser.Attach(port.Value);
            }

            throw new InvalidOperationException("Either Browser or Port must be provided.");
        }
    }

    internal static class CdpTabLocator
    {
        public static CdpTab Resolve(string selectorXml, CdpTab tab)
        {
            if (!string.IsNullOrWhiteSpace(selectorXml) &&
                Selectors.SelectorXmlSerializer.HasWndLevel(selectorXml))
            {
                return CdpTabFinder.FindTab(selectorXml).Tab;
            }

            if (tab != null)
            {
                return tab;
            }

            throw new InvalidOperationException(
                "Either Tab or a Selector XML containing <wnd> must be provided.");
        }

        public static CdpTab ResolveRequired(string selectorXml, CdpTab tab)
        {
            if (!string.IsNullOrWhiteSpace(selectorXml) &&
                Selectors.SelectorXmlSerializer.HasWndLevel(selectorXml))
            {
                return CdpTabFinder.FindTab(selectorXml).Tab;
            }

            if (tab != null)
            {
                return tab;
            }

            throw new InvalidOperationException("Tab or Selector with <wnd> is required.");
        }
    }

    internal static class CdpSelectorRules
    {
        public static void EnsureTabOrWnd(string selectorXml, CdpTab tab)
        {
            if (Selectors.SelectorXmlSerializer.HasWndLevel(selectorXml))
            {
                return;
            }

            if (tab == null)
            {
                throw new InvalidOperationException(
                    "Selector has no <wnd> level. Input Tab must be provided, or include a <wnd> line in Selector XML.");
            }
        }
    }

    internal static class CdpDelay
    {
        public static void Apply(int delayMs)
        {
            if (delayMs > 0)
            {
                Thread.Sleep(delayMs);
            }
        }
    }

    internal sealed class TimeoutBudget
    {
        private readonly int _totalMs;
        private readonly System.Diagnostics.Stopwatch _stopwatch;

        public TimeoutBudget(int totalMs)
        {
            _totalMs = Math.Max(0, totalMs);
            _stopwatch = System.Diagnostics.Stopwatch.StartNew();
        }

        public int RemainingMs
        {
            get
            {
                var remaining = _totalMs - (int)_stopwatch.ElapsedMilliseconds;
                return remaining > 0 ? remaining : 0;
            }
        }
    }
}
