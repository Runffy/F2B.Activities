using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using F2B.Browser.Chromium.Cdp.Exceptions;
using F2B.Browser.Chromium.Cdp.Internal;

namespace F2B.Browser.Chromium.Cdp.Browser
{
    /// <summary>
    /// A connected Chromium-based browser instance controlled via CDP.
    /// </summary>
    public class CdpBrowser : IDisposable
    {
        private readonly CdpBrowserConnection _connection;
        private readonly object _syncRoot = new object();
        private CdpDownloadManager _downloadManager;

        internal CdpBrowser(
            int port,
            string executablePath,
            string userDataDir,
            string browserName,
            Process process,
            bool attachedToExisting,
            string browserWebSocketUrl)
        {
            Port = port;
            ExecutablePath = executablePath;
            UserDataDir = userDataDir;
            BrowserName = browserName;
            Process = process;
            AttachedToExisting = attachedToExisting;
            BrowserWebSocketUrl = browserWebSocketUrl;
            _connection = new CdpBrowserConnection(browserWebSocketUrl);
        }

        public int Port { get; private set; }

        public string Address
        {
            get { return string.Format("127.0.0.1:{0}", Port); }
        }

        public string ExecutablePath { get; private set; }

        public string UserDataDir { get; private set; }

        public string BrowserName { get; private set; }

        public Process Process { get; private set; }

        public bool AttachedToExisting { get; private set; }

        public string BrowserWebSocketUrl { get; private set; }

        /// <summary>
        /// The most recently listed visible tab.
        /// </summary>
        public CdpTab LatestTab
        {
            get { return GetTab(); }
        }

        /// <summary>
        /// Visible tab target ids.
        /// </summary>
        public IList<string> TabIds
        {
            get { return QueryVisibleTargets().Select(target => target.Id).ToList(); }
        }

        /// <summary>
        /// Number of visible tabs.
        /// </summary>
        public int TabsCount
        {
            get { return TabIds.Count; }
        }

        /// <summary>
        /// Attaches to an existing Chrome/Edge CDP browser on the given port.
        /// </summary>
        public static CdpBrowser Attach(int port)
        {
            return Attach(port, null);
        }

        /// <summary>
        /// Gets a visible tab by id, 1-based index, title/url filter, or the first visible tab when no filter is provided.
        /// </summary>
        public CdpTab GetTab(object idOrNum = null, string title = null, string url = null)
        {
            if (idOrNum is CdpTab)
            {
                return EnsureVisibleTab((CdpTab)idOrNum);
            }

            string targetId = null;

            if (idOrNum != null)
            {
                targetId = ResolveTargetIdFromIdOrNum(idOrNum);
            }
            else if (title == null && url == null)
            {
                targetId = GetVisibleTabIdsInternal().FirstOrDefault();
            }
            else
            {
                var matched = GetTabs(title, url);
                if (matched.Count == 0)
                {
                    throw new BrowserException(BuildNoSuchTabMessage(idOrNum, title, url));
                }

                return matched[0];
            }

            if (string.IsNullOrEmpty(targetId))
            {
                throw new BrowserException(BuildNoSuchTabMessage(idOrNum, title, url));
            }

            return CreateTab(targetId);
        }

        /// <summary>
        /// Gets visible tabs filtered by title and/or url.
        /// </summary>
        public IList<CdpTab> GetTabs(string title = null, string url = null)
        {
            return QueryVisibleTargets()
                .Where(target => MatchesTabFilter(target, title, url))
                .Select(CreateTab)
                .ToList();
        }

        /// <summary>
        /// Activates a visible tab by target id, 1-based index, or <see cref="CdpTab"/> instance.
        /// </summary>
        public void ActivateTab(object idOrNumOrTab)
        {
            var targetId = ResolveActivateTargetId(idOrNumOrTab);
            _connection.ActivateTarget(targetId);
        }

        /// <summary>
        /// Creates a new visible tab. Optionally navigates to the given url.
        /// </summary>
        public CdpTab NewTab(
            string url = null,
            bool newWindow = false,
            bool background = false,
            bool newContext = false)
        {
            string browserContextId = null;
            if (newContext)
            {
                browserContextId = _connection.CreateBrowserContext();
            }

            var targetId = _connection.CreateTarget(
                string.IsNullOrWhiteSpace(url) ? string.Empty : url,
                newWindow,
                background,
                browserContextId);

            WaitForVisibleTab(targetId);

            if (!string.IsNullOrWhiteSpace(url))
            {
                var tab = CreateTab(targetId);
                // Target.createTarget already opened the URL; wait for load then refresh metadata.
                tab.WaitForDocumentComplete(CdpDocumentWaitScope.MainDocument, 15000);
                RefreshTab(tab);
                return tab;
            }

            return CreateTab(targetId);
        }

        /// <summary>
        /// Closes a visible tab by target id or <see cref="CdpTab"/> instance.
        /// </summary>
        public void CloseTab(object tabOrId)
        {
            var targetId = ResolveTargetIdFromTabOrId(tabOrId);
            EnsureVisibleTargetId(targetId);
            _connection.CloseTarget(targetId);
            WaitUntilTabClosed(targetId);
        }

        /// <summary>
        /// Closes the browser process when this instance launched it.
        /// </summary>
        public void Quit()
        {
            if (AttachedToExisting)
            {
                return;
            }

            try
            {
                _connection.CloseBrowser();
            }
            catch
            {
                // Browser may already be closing.
            }

            TryKillOwnedProcess();
        }

        public void Dispose()
        {
            if (!AttachedToExisting)
            {
                TryKillOwnedProcess();
            }

            if (_connection != null)
            {
                _connection.Dispose();
            }
        }

        private void TryKillOwnedProcess()
        {
            if (Process == null)
            {
                return;
            }

            try
            {
                if (!Process.HasExited)
                {
                    if (!Process.WaitForExit(3000))
                    {
                        ProcessHelper.KillProcessOnPort(Port);
                    }
                }
            }
            catch
            {
                try
                {
                    ProcessHelper.KillProcessOnPort(Port);
                }
                catch
                {
                    // Best effort cleanup.
                }
            }
        }

        internal static CdpBrowser FromOpenResult(BrowserOpenResult result)
        {
            var version = CdpJsonClient.GetBrowserVersion(result.Port);
            return new CdpBrowser(
                result.Port,
                result.ExecutablePath,
                result.UserDataDir,
                result.BrowserName,
                result.Process,
                result.AttachedToExisting,
                version.WebSocketDebuggerUrl);
        }

        internal static CdpBrowser Attach(int port, CdpBrowserVersionInfo versionInfo)
        {
            var version = versionInfo ?? CdpJsonClient.GetBrowserVersion(port);
            if (!CdpConnectionChecker.CanConnect(port))
            {
                throw new BrowserException(string.Format("Unable to connect to CDP browser on port {0}.", port));
            }

            if (!BrowserNameHelper.IsSupportedBrowser(version.BrowserName))
            {
                throw new BrowserException(
                    string.Format("Port {0} is not a supported Chrome/Edge CDP browser.", port));
            }

            return new CdpBrowser(
                port,
                string.Empty,
                string.Empty,
                version.BrowserName,
                null,
                true,
                version.WebSocketDebuggerUrl);
        }

        internal CdpTab CreateTabFromTarget(CdpTargetInfo target)
        {
            if (!VisibleTabFilter.IsVisibleTab(target))
            {
                throw new BrowserException(string.Format("Target is not a visible tab: {0}", target.Id));
            }

            return CreateTab(target);
        }

        private IList<CdpTargetInfo> QueryVisibleTargets()
        {
            lock (_syncRoot)
            {
                return VisibleTabFilter.ListVisibleTabs(Port);
            }
        }

        private IList<string> GetVisibleTabIdsInternal()
        {
            return QueryVisibleTargets().Select(target => target.Id).ToList();
        }

        private CdpTab CreateTab(string targetId)
        {
            var target = QueryVisibleTargets().FirstOrDefault(item => item.Id == targetId);
            if (target == null)
            {
                throw new BrowserException(
                    string.Format("Visible tab not found: {0}. Current visible tabs: [{1}]", targetId, string.Join(", ", TabIds)));
            }

            return CreateTab(target);
        }

        private CdpTab CreateTab(CdpTargetInfo target)
        {
            return new CdpTab(this, target.Id, target.Title, target.Url, target.WebSocketDebuggerUrl);
        }

        private CdpTab EnsureVisibleTab(CdpTab tab)
        {
            if (tab == null)
            {
                throw new ArgumentNullException("tab");
            }

            if (!GetVisibleTabIdsInternal().Contains(tab.Id))
            {
                throw new BrowserException(string.Format("Tab is not a visible tab: {0}", tab.Id));
            }

            return tab;
        }

        private void EnsureVisibleTargetId(string targetId)
        {
            if (!GetVisibleTabIdsInternal().Contains(targetId))
            {
                throw new BrowserException(string.Format("Target is not a visible tab: {0}", targetId));
            }
        }

        private string ResolveTargetIdFromIdOrNum(object idOrNum)
        {
            var stringId = idOrNum as string;
            if (stringId != null)
            {
                EnsureVisibleTargetId(stringId);
                return stringId;
            }

            if (idOrNum is int)
            {
                return ResolveTargetIdFromIndex((int)idOrNum);
            }

            throw new BrowserException(
                string.Format("Unsupported tab identifier type: {0}", idOrNum.GetType().Name));
        }

        private string ResolveActivateTargetId(object idOrNumOrTab)
        {
            if (idOrNumOrTab is CdpTab)
            {
                return EnsureVisibleTab((CdpTab)idOrNumOrTab).Id;
            }

            if (idOrNumOrTab is int)
            {
                var index = (int)idOrNumOrTab;
                index = index != 0 ? index - 1 : index + 1;
                var tabIds = GetVisibleTabIdsInternal();
                if (index < 0 || index >= tabIds.Count)
                {
                    throw new BrowserException(
                        string.Format("Visible tab index out of range: {0}. Current visible tab count: {1}", idOrNumOrTab, tabIds.Count));
                }

                return tabIds[index];
            }

            var stringId = idOrNumOrTab as string;
            if (stringId != null)
            {
                return ResolveTargetIdFromIdOrNum(stringId);
            }

            throw new BrowserException(
                string.Format("Unsupported tab identifier type: {0}", idOrNumOrTab.GetType().Name));
        }

        private string ResolveTargetIdFromIndex(int index)
        {
            var tabIds = GetVisibleTabIdsInternal();
            var normalizedIndex = index > 0 ? index - 1 : index;
            if (normalizedIndex < 0 || normalizedIndex >= tabIds.Count)
            {
                throw new BrowserException(
                    string.Format("Visible tab index out of range: {0}. Current visible tab count: {1}", index, tabIds.Count));
            }

            return tabIds[normalizedIndex];
        }

        private static string ResolveTargetIdFromTabOrId(object tabOrId)
        {
            var tab = tabOrId as CdpTab;
            if (tab != null)
            {
                return tab.Id;
            }

            var stringId = tabOrId as string;
            if (stringId != null)
            {
                return stringId;
            }

            throw new BrowserException(
                string.Format("Unsupported tab identifier type: {0}", tabOrId.GetType().Name));
        }

        private static bool MatchesTabFilter(CdpTargetInfo target, string title, string url)
        {
            if (title != null && (target.Title == null || target.Title.IndexOf(title, StringComparison.OrdinalIgnoreCase) < 0))
            {
                return false;
            }

            if (url != null && (target.Url == null || target.Url.IndexOf(url, StringComparison.OrdinalIgnoreCase) < 0))
            {
                return false;
            }

            return true;
        }

        private static string BuildNoSuchTabMessage(object idOrNum, string title, string url)
        {
            return string.Format(
                "No matching visible tab found. idOrNum={0}, title={1}, url={2}",
                idOrNum,
                title,
                url);
        }

        private void WaitForVisibleTab(string targetId)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                if (GetVisibleTabIdsInternal().Contains(targetId))
                {
                    return;
                }

                Thread.Sleep(50);
            }

            throw new BrowserException(string.Format("Timed out waiting for visible tab: {0}", targetId));
        }

        private void WaitUntilTabClosed(string targetId)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                if (!GetVisibleTabIdsInternal().Contains(targetId))
                {
                    return;
                }

                Thread.Sleep(50);
            }
        }

        private void NavigateTab(CdpTab tab, string url)
        {
            if (string.IsNullOrWhiteSpace(tab.WebSocketDebuggerUrl))
            {
                throw new BrowserException(string.Format("Tab {0} has no WebSocketDebuggerUrl.", tab.Id));
            }

            CdpSession.Navigate(tab.WebSocketDebuggerUrl, url, TimeSpan.FromSeconds(10));
        }

        private void RefreshTab(CdpTab tab)
        {
            TryRefreshTab(tab);
        }

        internal bool TryRefreshTab(CdpTab tab)
        {
            if (tab == null)
            {
                return false;
            }

            var target = QueryVisibleTargets().FirstOrDefault(item => item.Id == tab.Id);
            if (target == null)
            {
                return false;
            }

            tab.Refresh(target.Title, target.Url, target.WebSocketDebuggerUrl);
            return true;
        }

        internal Dictionary<string, object> GetWindowBounds(string targetId)
        {
            var result = _connection.GetWindowForTarget(targetId);
            var bounds = CdpValueConverter.GetDictionary(result, "bounds");
            return bounds ?? new Dictionary<string, object>();
        }

        /// <summary>
        /// Maximizes the browser window that contains the given tab, or <see cref="LatestTab"/> when omitted.
        /// </summary>
        public void Maximize(CdpTab tab = null)
        {
            CdpBrowserWindowHelper.Maximize(_connection, ResolveWindowReferenceTab(tab).Id);
        }

        /// <summary>
        /// Minimizes the browser window that contains the given tab, or <see cref="LatestTab"/> when omitted.
        /// </summary>
        public void Minimize(CdpTab tab = null)
        {
            CdpBrowserWindowHelper.Minimize(_connection, ResolveWindowReferenceTab(tab).Id);
        }

        /// <summary>
        /// Restores the browser window that contains the given tab to normal state, or <see cref="LatestTab"/> when omitted.
        /// </summary>
        public void Normal(CdpTab tab = null)
        {
            CdpBrowserWindowHelper.Normal(_connection, ResolveWindowReferenceTab(tab).Id);
        }

        internal void SetWindowFullscreen(CdpTab tab)
        {
            if (tab == null)
            {
                throw new ArgumentNullException("tab");
            }

            CdpBrowserWindowHelper.Fullscreen(_connection, tab.Id);
        }

        private CdpTab ResolveWindowReferenceTab(CdpTab tab)
        {
            return tab ?? LatestTab;
        }

        internal CdpDownloadManager GetDownloadManager()
        {
            lock (_syncRoot)
            {
                return _downloadManager ?? (_downloadManager = new CdpDownloadManager(this));
            }
        }

        internal CdpTab WaitForNewTab(ISet<string> existingTabIds, int timeoutMs)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs <= 0 ? 3000 : timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                foreach (var tabId in TabIds)
                {
                    if (!existingTabIds.Contains(tabId))
                    {
                        return GetTab(tabId);
                    }
                }

                Thread.Sleep(50);
            }

            throw new BrowserException("No new tab was opened.");
        }
    }
}
