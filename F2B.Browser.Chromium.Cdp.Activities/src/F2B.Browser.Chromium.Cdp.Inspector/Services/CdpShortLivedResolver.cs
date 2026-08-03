using System;
using System.Linq;
using F2B.Browser.Chromium.Cdp.Browser;
using F2B.Browser.Chromium.Cdp.Selectors;

namespace F2B.Browser.Chromium.Cdp.Inspector.Services
{
    /// <summary>
    /// Short-lived Attach sessions for Validate / Highlight. Never kills the browser process.
    /// </summary>
    internal static class CdpShortLivedResolver
    {
        public sealed class ResolveResult
        {
            public int MatchCount { get; set; }

            public CdpElement FirstElement { get; set; }

            public CdpTab Tab { get; set; }

            public CdpBrowser Browser { get; set; }

            public string Error { get; set; }
        }

        /// <summary>
        /// Attaches, finds matches, then disposes the browser connection (attach-only; process kept alive).
        /// When <paramref name="keepAlive"/> is returned to the caller, the caller must dispose browser.
        /// </summary>
        public static ResolveResult CountMatches(string selectorXml, bool keepSessionAlive = false)
        {
            var result = new ResolveResult();
            if (string.IsNullOrWhiteSpace(selectorXml))
            {
                result.Error = "Selector is empty.";
                return result;
            }

            try
            {
                var scope = SelectorXmlSerializer.SplitScope(selectorXml);
                if (scope.TabLevel == null)
                {
                    result.Error = "Selector must contain a <wnd> level.";
                    return result;
                }

                var port = ReadPort(scope.TabLevel);
                if (port.HasValue && port.Value > 0)
                {
                    result.Browser = CdpBrowser.Attach(port.Value);
                    result.Tab = FindMatchingTab(result.Browser, scope.TabLevel) ?? result.Browser.LatestTab;
                }
                else
                {
                    // Prefer process command-line discovery (fast) over probing every TCP port.
                    result = ResolveWithoutPort(scope, selectorXml, keepSessionAlive);
                    return result;
                }

                if (result.Tab == null)
                {
                    result.Error = "No matching tab.";
                    DisposeQuietly(result.Browser);
                    result.Browser = null;
                    return result;
                }

                var operationXml = SelectorXmlSerializer.ToOperationXml(scope);
                if (string.IsNullOrWhiteSpace(operationXml))
                {
                    result.MatchCount = 1;
                    result.FirstElement = null;
                }
                else
                {
                    ApplyElementMatches(result, operationXml, keepSessionAlive);
                }

                if (!keepSessionAlive)
                {
                    DisposeQuietly(result.Browser);
                    result.Browser = null;
                    result.Tab = null;
                    result.FirstElement = null;
                }

                return result;
            }
            catch (Exception ex)
            {
                DisposeQuietly(result.Browser);
                result.Browser = null;
                result.Tab = null;
                result.FirstElement = null;
                result.MatchCount = 0;
                result.Error = ex.Message;
                return result;
            }
        }

        public static void DisposeQuietly(CdpBrowser browser)
        {
            if (browser == null)
            {
                return;
            }

            try
            {
                // Attach sessions set AttachedToExisting=true, so Dispose only closes the websocket.
                browser.Dispose();
            }
            catch
            {
            }
        }

        private static ResolveResult ResolveWithoutPort(
            SelectorScope scope,
            string selectorXml,
            bool keepSessionAlive)
        {
            var result = new ResolveResult();
            Exception lastError = null;

            foreach (var discovered in CdpBrowserDiscovery.ListDebuggingBrowsersFromProcesses())
            {
                CdpBrowser browser = null;
                try
                {
                    browser = CdpBrowser.Attach(discovered.Port);
                    var tab = FindMatchingTab(browser, scope.TabLevel);
                    if (tab == null)
                    {
                        DisposeQuietly(browser);
                        continue;
                    }

                    result.Browser = browser;
                    result.Tab = tab;

                    var operationXml = SelectorXmlSerializer.ToOperationXml(scope);
                    if (string.IsNullOrWhiteSpace(operationXml))
                    {
                        result.MatchCount = 1;
                    }
                    else
                    {
                        ApplyElementMatches(result, operationXml, keepSessionAlive);
                    }

                    if (result.MatchCount > 0)
                    {
                        if (!keepSessionAlive)
                        {
                            DisposeQuietly(result.Browser);
                            result.Browser = null;
                            result.Tab = null;
                            result.FirstElement = null;
                        }

                        return result;
                    }

                    DisposeQuietly(browser);
                    result.Browser = null;
                    result.Tab = null;
                    result.FirstElement = null;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    DisposeQuietly(browser);
                }
            }

            // Fallback to full tab finder (may scan ports) if process discovery found nothing.
            try
            {
                var found = CdpTabFinder.FindTab(selectorXml);
                result.Browser = found.Browser;
                result.Tab = found.Tab;
                var operationXml = SelectorXmlSerializer.ToOperationXml(scope);
                if (string.IsNullOrWhiteSpace(operationXml))
                {
                    result.MatchCount = 1;
                }
                else
                {
                    ApplyElementMatches(result, operationXml, keepSessionAlive);
                }

                if (!keepSessionAlive)
                {
                    DisposeQuietly(result.Browser);
                    result.Browser = null;
                    result.Tab = null;
                    result.FirstElement = null;
                }

                return result;
            }
            catch (Exception ex)
            {
                result.Error = lastError != null ? lastError.Message : ex.Message;
                result.MatchCount = 0;
                return result;
            }
        }

        /// <summary>
        /// Count matches via FindElements (objectId array path; supports parent / multi-match).
        /// </summary>
        private static void ApplyElementMatches(
            ResolveResult result,
            string operationXml,
            bool keepSessionAlive)
        {
            var elements = result.Tab.FindElements(operationXml);
            if (elements != null && elements.Length > 0)
            {
                result.MatchCount = elements.Length;
                result.FirstElement = keepSessionAlive ? elements[0] : null;
                return;
            }

            result.MatchCount = 0;
            result.FirstElement = null;
        }

        private static int? ReadPort(SelectorLevel wndLevel)
        {
            if (wndLevel == null)
            {
                return null;
            }

            var portProp = wndLevel.Properties.FirstOrDefault(p =>
                string.Equals(p.Name, "port", StringComparison.OrdinalIgnoreCase) &&
                p.IsSelected &&
                !string.IsNullOrWhiteSpace(p.Value));
            if (portProp == null)
            {
                return null;
            }

            int port;
            return int.TryParse(portProp.Value, out port) ? port : (int?)null;
        }

        private static CdpTab FindMatchingTab(CdpBrowser browser, SelectorLevel wndLevel)
        {
            if (browser == null)
            {
                return null;
            }

            var title = GetProp(wndLevel, "title");
            var url = GetProp(wndLevel, "url");
            var hasFilter = !string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(url);
            if (!hasFilter)
            {
                return browser.LatestTab;
            }

            var tabs = browser.GetTabs(
                string.IsNullOrWhiteSpace(title) ? null : title,
                string.IsNullOrWhiteSpace(url) ? null : url);
            if (tabs != null && tabs.Count > 0)
            {
                return tabs[0];
            }

            // Filters were specified but nothing matched — do not silently use LatestTab.
            return null;
        }

        private static string GetProp(SelectorLevel level, string name)
        {
            if (level == null)
            {
                return null;
            }

            var prop = level.Properties.FirstOrDefault(p =>
                string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase) &&
                p.IsSelected &&
                !p.IsRegex &&
                !string.IsNullOrWhiteSpace(p.Value));
            return prop == null ? null : prop.Value;
        }
    }
}
