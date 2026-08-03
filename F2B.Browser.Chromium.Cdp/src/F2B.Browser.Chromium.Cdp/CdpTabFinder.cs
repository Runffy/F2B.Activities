using System.Collections.Generic;
using F2B.Browser.Chromium.Cdp.Browser;
using F2B.Browser.Chromium.Cdp.Exceptions;
using F2B.Browser.Chromium.Cdp.Internal;
using F2B.Browser.Chromium.Cdp.Selectors;

namespace F2B.Browser.Chromium.Cdp
{
    /// <summary>
    /// Standalone helpers for locating visible tabs across CDP-connected browsers.
    /// </summary>
    public static class CdpTabFinder
    {
        /// <summary>
        /// Finds a visible tab matching the selector XML across Chrome/Edge CDP browsers.
        /// </summary>
        /// <param name="selectorXml">
        /// Example: &lt;wnd title-re="百度一下，你就知道.*" browser="chrome" port="9222" idx="0" /&gt;
        /// May also include additional &lt;frm&gt;/&lt;ctrl&gt; levels; only the &lt;wnd&gt; line is used for tab matching.
        /// Supported attributes: title, title-re, url, url-re, browser, port, idx.
        /// When multiple tabs match, idx (0-based) selects which tab to return. Defaults to 0.
        /// </param>
        public static CdpTabFindResult FindTab(string selectorXml)
        {
            var scope = SelectorXmlSerializer.SplitScope(selectorXml);
            if (scope.TabLevel == null)
            {
                throw new BrowserException("Selector XML must contain a <wnd> level.");
            }

            var wndXml = SelectorXmlSerializer.SerializeLevelTag(scope.TabLevel);
            var selector = TabSelector.Parse(wndXml);
            var ports = CdpPortDiscovery.DiscoverBrowserPorts(selector.Port);
            var matches = new List<CdpTabFindResult>();

            foreach (var port in ports)
            {
                CdpBrowserVersionInfo version;
                try
                {
                    version = CdpJsonClient.GetBrowserVersion(port);
                }
                catch
                {
                    continue;
                }

                if (!BrowserNameHelper.IsSupportedBrowser(version.BrowserName))
                {
                    continue;
                }

                if (!selector.MatchesBrowser(version.BrowserName))
                {
                    continue;
                }

                var browser = CdpBrowser.Attach(port, version);
                foreach (var target in VisibleTabFilter.ListVisibleTabs(port))
                {
                    if (!selector.MatchesTab(target))
                    {
                        continue;
                    }

                    matches.Add(new CdpTabFindResult(browser, browser.CreateTabFromTarget(target)));
                }
            }

            if (matches.Count == 0)
            {
                throw new BrowserException(string.Format("No visible tab matched selector: {0}", selectorXml));
            }

            if (selector.Idx >= matches.Count)
            {
                throw new BrowserException(
                    string.Format(
                        "Selector idx {0} is out of range. {1} visible tab(s) matched: {2}",
                        selector.Idx,
                        matches.Count,
                        selectorXml));
            }

            return matches[selector.Idx];
        }
    }
}
