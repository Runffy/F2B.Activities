using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using F2B.Browser.Chromium.Cdp.Exceptions;
using F2B.Browser.Chromium.Cdp.Internal;

namespace F2B.Browser.Chromium.Cdp.Browser
{
    /// <summary>
    /// Represents a single browser tab/page target attached to a CDP browser.
    /// </summary>
    public class CdpTab : CdpBase, IDisposable
    {
        private readonly object _syncRoot = new object();
        private CdpTabSession _session;
        private CdpTabStates _states;
        private CdpTabRect _rect;

        internal CdpTab(CdpBrowser browser, string id, string title, string url, string webSocketDebuggerUrl)
        {
            Browser = browser;
            Id = id;
            _title = title ?? string.Empty;
            _url = url ?? string.Empty;
            WebSocketDebuggerUrl = webSocketDebuggerUrl ?? string.Empty;
        }

        public override CdpTab Tab
        {
            get { return this; }
        }

        /// <summary>The <see cref="CdpBrowser"/> instance that owns this tab.</summary>
        public CdpBrowser Browser { get; private set; }

        /// <summary>
        /// Enters browser fullscreen for this tab's window (equivalent to pressing F11).
        /// </summary>
        public void Full()
        {
            Browser.SetWindowFullscreen(this);
        }

        /// <summary>Tab target id.</summary>
        public string Id { get; private set; }

        /// <summary>Alias for <see cref="Id"/>.</summary>
        public string TabId
        {
            get { return Id; }
        }

        /// <summary>Current page title.</summary>
        public string Title
        {
            get
            {
                RefreshFromTarget();
                return _title;
            }
        }

        /// <summary>Current page url.</summary>
        public string Url
        {
            get
            {
                RefreshFromTarget();
                return _url;
            }
        }

        public string WebSocketDebuggerUrl { get; private set; }

        /// <summary>Main document HTML (iframe tags only, not frame internals).</summary>
        public string Html
        {
            get { return GetSession().GetHtml(); }
        }

        /// <summary>Current user agent string.</summary>
        public string UserAgent
        {
            get { return GetSession().GetUserAgent(); }
        }

        /// <summary>Cookies for the current page url.</summary>
        public CdpCookies Cookies
        {
            get { return GetSession().GetCookies(); }
        }

        /// <summary>Session storage for the current page.</summary>
        public Dictionary<string, string> SessionStorage
        {
            get { return GetSession().GetSessionStorage(); }
        }

        /// <summary>Local storage for the current page.</summary>
        public Dictionary<string, string> LocalStorage
        {
            get { return GetSession().GetLocalStorage(); }
        }

        /// <summary>Page loading and availability states.</summary>
        public CdpTabStates States
        {
            get { return _states ?? (_states = new CdpTabStates(this)); }
        }

        /// <summary>Page and window geometry.</summary>
        public CdpTabRect Rect
        {
            get { return _rect ?? (_rect = new CdpTabRect(this)); }
        }

        /// <summary>
        /// Sends a GET request in the tab context via synchronous XHR.
        /// </summary>
        public CdpResponse Get(string url)
        {
            return GetSession().Get(url);
        }

        /// <summary>
        /// Sends a POST request in the tab context via synchronous XHR.
        /// Pass <paramref name="data"/> directly, or pass <paramref name="dict"/> to urlencode the form payload first.
        /// </summary>
        public CdpResponse Post(string url, string data = null, Dictionary<string, object> dict = null)
        {
            return GetSession().Post(url, data, dict);
        }

        /// <summary>
        /// Executes JavaScript in the tab context.
        /// In non-expression mode, arguments are available as <c>arguments[0]</c>, <c>arguments[1]</c>, ...
        /// </summary>
        /// <param name="script">JavaScript source or expression.</param>
        /// <param name="args">Optional arguments passed to the script.</param>
        /// <param name="asExpression">When true, <paramref name="script"/> is evaluated as an expression and <paramref name="args"/> are ignored.</param>
        /// <param name="isAsync">When true, executes without waiting for the result and returns null immediately.</param>
        /// <param name="timeoutMs">Timeout in milliseconds when <paramref name="isAsync"/> is false.</param>
        public override object RunJs(
            string script,
            object[] args = null,
            bool asExpression = false,
            bool isAsync = false,
            int timeoutMs = 30000)
        {
            return GetSession().RunJs(script, args, asExpression, isAsync, timeoutMs);
        }

        /// <summary>
        /// Finds the first element matching selector XML within the given timeout.
        /// When timeout is 0, performs a single instantaneous lookup.
        /// </summary>
        /// <param name="throwException">When false, returns null instead of throwing if no element is found.</param>
        public override CdpElement FindElement(string selectorXml, int timeoutMs = 15000, bool throwException = true)
        {
            return SelectorElementFinder.FindElement(this, selectorXml, timeoutMs, throwException);
        }

        /// <summary>
        /// Finds all elements matching selector XML using an instantaneous page snapshot.
        /// </summary>
        public override CdpElement[] FindElements(string selectorXml)
        {
            return SelectorElementFinder.FindElements(this, selectorXml).ToArray();
        }

        /// <summary>
        /// Instantly checks whether an element matching selector XML exists.
        /// </summary>
        public override bool ElementExists(string selectorXml)
        {
            return SelectorElementFinder.TryFindElement(this, selectorXml);
        }

        /// <summary>
        /// Finds a frame matching selector XML (supports nested &lt;frm&gt;).
        /// </summary>
        public override CdpFrame FindFrame(string selectorXml, int timeoutMs = 15000, bool throwException = true)
        {
            return CdpFrameResolver.FindFrame(this, null, null, selectorXml, timeoutMs, throwException);
        }

        /// <summary>
        /// Waits until the requested document scope reaches <c>readyState === 'complete'</c>.
        /// </summary>
        /// <param name="scope">
        /// <see cref="CdpDocumentWaitScope.MainDocument"/> waits for the main frame only;
        /// <see cref="CdpDocumentWaitScope.AllDocuments"/> waits for the main frame and every frame in the frame tree.
        /// </param>
        /// <param name="timeoutMs">Maximum wait time in milliseconds.</param>
        public void WaitForDocumentComplete(
            CdpDocumentWaitScope scope = CdpDocumentWaitScope.MainDocument,
            int timeoutMs = 15000)
        {
            GetSession().WaitForDocumentComplete(scope, timeoutMs);
        }

        /// <summary>Navigates the tab to the given url.</summary>
        public void Navigate(string url)
        {
            GetSession().Navigate(url);
        }

        /// <summary>Reloads the current page.</summary>
        /// <param name="ignoreCache">When true, bypasses the browser cache.</param>
        public void Refresh(bool ignoreCache = false)
        {
            GetSession().Refresh(ignoreCache);
        }

        /// <summary>Goes back in the navigation history.</summary>
        /// <param name="steps">Number of history entries to go back.</param>
        public void Back(int steps = 1)
        {
            GetSession().Back(steps);
        }

        /// <summary>Goes forward in the navigation history.</summary>
        /// <param name="steps">Number of history entries to go forward.</param>
        public void Forward(int steps = 1)
        {
            GetSession().Forward(steps);
        }

        /// <summary>
        /// Captures a screenshot of the current viewport, or the full scrollable page when <paramref name="fullPage"/> is true.
        /// </summary>
        public byte[] GetScreenshot(bool fullPage = false)
        {
            return CdpScreenshotCapture.CaptureTab(this, fullPage);
        }

        /// <summary>
        /// Saves a screenshot of the current viewport, or the full scrollable page when <paramref name="fullPage"/> is true.
        /// </summary>
        public void SaveScreenshot(string path, bool fullPage = false)
        {
            CdpScreenshotCapture.SaveToFile(GetScreenshot(fullPage), path);
        }

        /// <summary>
        /// Polls multiple selector XML strings in parallel and returns the first match.
        /// Each poll cycle checks every selector instantaneously.
        /// </summary>
        public CdpParallelFindElementResult ParallelFindElement(IList<string> selectorXmlList, int timeoutMs)
        {
            return SelectorElementFinder.ParallelFindElement(this, selectorXmlList, timeoutMs);
        }

        /// <summary>
        /// Polls multiple selector XML strings in parallel and returns the first match.
        /// </summary>
        public CdpParallelFindElementResult ParallelFindElement(string[] selectorXmlList, int timeoutMs)
        {
            return SelectorElementFinder.ParallelFindElement(this, selectorXmlList, timeoutMs);
        }

        internal CdpTabSession GetSession()
        {
            lock (_syncRoot)
            {
                return _session ?? (_session = new CdpTabSession(this));
            }
        }

        internal CdpTabStateSnapshot SessionQueryStates()
        {
            return GetSession().GetStates();
        }

        internal CdpTabRectSnapshot SessionQueryRect()
        {
            return GetSession().GetRect();
        }

        internal void Refresh(string title, string url, string webSocketDebuggerUrl)
        {
            _title = title ?? string.Empty;
            _url = url ?? string.Empty;
            WebSocketDebuggerUrl = webSocketDebuggerUrl ?? string.Empty;
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                if (_session != null)
                {
                    _session.Dispose();
                    _session = null;
                }
            }
        }

        public override string ToString()
        {
            return string.Format("CdpTab(Id={0}, Title={1}, Url={2})", Id, Title, Url);
        }

        private string _title;
        private string _url;

        private void RefreshFromTarget()
        {
            if (Browser == null)
            {
                return;
            }

            try
            {
                var targetInfo = GetSession().GetTargetInfo();
                var info = CdpValueConverter.GetDictionary(targetInfo, "targetInfo");
                if (info != null)
                {
                    Refresh(
                        CdpValueConverter.GetString(info, "title"),
                        CdpValueConverter.GetString(info, "url"),
                        WebSocketDebuggerUrl);
                    return;
                }
            }
            catch (BrowserException)
            {
                // Fall back to target list refresh.
            }

            Browser.TryRefreshTab(this);
        }
    }
}
