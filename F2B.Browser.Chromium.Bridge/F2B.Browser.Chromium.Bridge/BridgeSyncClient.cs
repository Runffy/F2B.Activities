using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using F2B.Browser.Chromium.Bridge.Selectors;

namespace F2B.Browser.Chromium.Bridge
{
    public sealed class BridgeSyncClient : IDisposable
    {
        private readonly IBridgeRpcChannel _rpc;
        private readonly bool _ownsRpc;
        private readonly string _instanceId;

        public BridgeSyncClient(BridgeWebSocketServer server, string instanceId)
            : this(new BridgeRpcHost(server), instanceId, true)
        {
        }

        public BridgeSyncClient(IBridgeRpcChannel rpc, string instanceId, bool ownsRpc)
        {
            if (rpc == null)
                throw new ArgumentNullException(nameof(rpc));

            _rpc = rpc;
            _ownsRpc = ownsRpc;
            _instanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));
        }

        public string InstanceId
        {
            get { return _instanceId; }
        }

        public BwBrowser GetBrowser()
        {
            return new BwBrowser(_rpc, _instanceId);
        }

        public string GetElementText(string selectorXml, int timeoutMs = 15000)
        {
            var scope = SelectorXmlSerializer.SplitScope(selectorXml);
            if (scope.TabLevel != null)
            {
                throw new InvalidOperationException(
                    "Selector contains <wnd>. Use BridgeHost.ResolveContext() or BridgeHost.GetElementText().");
            }

            return GetElementText(scope, null, timeoutMs);
        }

        public string GetElementText(BridgeExecutionContext context, int timeoutMs = 15000)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (!string.Equals(context.InstanceId, _instanceId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("BridgeExecutionContext instanceId does not match this client.");

            return GetElementText(context.Scope, context.Tab, timeoutMs);
        }

        public string GetElementText(SelectorScope scope, BwTab tab, int timeoutMs = 15000)
        {
            var findTimeoutMs = Math.Min(timeoutMs, 8000);
            var rpcTimeoutMs = timeoutMs + 3000;
            var response = Invoke(
                "element.getText",
                BridgeRpcHost.WithScopeForTab(scope, tab, new Dictionary<string, object>
                {
                    { "findTimeout", findTimeoutMs },
                    { "timeout", findTimeoutMs }
                }),
                rpcTimeoutMs);

            return BridgeJson.GetString(response.Data, "text");
        }

        public void ClickElement(string selectorXml, int timeoutMs = 15000)
        {
            var scope = SelectorXmlSerializer.SplitScope(selectorXml);
            if (scope.TabLevel != null)
            {
                throw new InvalidOperationException(
                    "Selector contains <wnd>. Use BridgeHost.ResolveContext() or BridgeHost.ClickElement().");
            }

            ClickElement(scope, null, timeoutMs);
        }

        public void ClickElement(BridgeExecutionContext context, int timeoutMs = 15000)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (!string.Equals(context.InstanceId, _instanceId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("BridgeExecutionContext instanceId does not match this client.");

            ClickElement(context.Scope, context.Tab, timeoutMs);
        }

        public void ClickElement(SelectorScope scope, BwTab tab, int timeoutMs = 15000)
        {
            var findTimeoutMs = Math.Min(timeoutMs, 8000);
            var rpcTimeoutMs = timeoutMs + 3000;
            Invoke(
                "element.click",
                BridgeRpcHost.WithScopeForTab(scope, tab, new Dictionary<string, object>
                {
                    { "findTimeout", findTimeoutMs },
                    { "timeout", findTimeoutMs }
                }),
                rpcTimeoutMs);
        }

        public bool ElementExists(string selectorXml, BwTab tab = null, int index = 0)
        {
            var scope = SelectorXmlSerializer.SplitScope(selectorXml);
            if (scope.TabLevel != null)
            {
                throw new InvalidOperationException(
                    "Selector contains <wnd>. Use BridgeHost.ElementExists().");
            }

            return ElementExists(scope, tab, index);
        }

        public bool ElementExists(BridgeExecutionContext context, int index = 0)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (!string.Equals(context.InstanceId, _instanceId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("BridgeExecutionContext instanceId does not match this client.");

            return ElementExists(context.Scope, context.Tab, index);
        }

        public bool ElementExists(SelectorScope scope, BwTab tab, int index = 0)
        {
            var response = Invoke(
                "tab.elementExists",
                BridgeRpcHost.WithScopeForTab(scope, tab, new Dictionary<string, object>
                {
                    { "index", index }
                }),
                15000);

            return BridgeJson.GetBool(response.Data, "exists");
        }

        public BwElement FindElement(
            string selectorXml,
            BwTab tab = null,
            int index = 0,
            int timeoutMs = 15000,
            BridgeFindElementWaitState waitState = BridgeFindElementWaitState.Attached,
            int delayBefore = 300)
        {
            var scope = SelectorXmlSerializer.SplitScope(selectorXml);
            if (scope.TabLevel != null)
            {
                throw new InvalidOperationException(
                    "Selector contains <wnd>. Use BridgeHost.FindElement().");
            }

            return FindElement(scope, tab, index, timeoutMs, waitState, delayBefore);
        }

        public BwElement FindElement(
            BridgeExecutionContext context,
            int index = 0,
            int timeoutMs = 15000,
            BridgeFindElementWaitState waitState = BridgeFindElementWaitState.Attached,
            int delayBefore = 300)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (!string.Equals(context.InstanceId, _instanceId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("BridgeExecutionContext instanceId does not match this client.");

            return FindElement(context.Scope, context.Tab, index, timeoutMs, waitState, delayBefore);
        }

        public BwElement FindElement(
            SelectorScope scope,
            BwTab tab,
            int index = 0,
            int timeoutMs = 15000,
            BridgeFindElementWaitState waitState = BridgeFindElementWaitState.Attached,
            int delayBefore = 300)
        {
            if (tab == null)
                throw new ArgumentNullException(nameof(tab));

            Invoke(
                "element.find",
                BridgeRpcHost.WithScopeForTab(scope, tab, new Dictionary<string, object>
                {
                    { "index", index },
                    { "timeout", timeoutMs },
                    { "waitState", waitState.ToString() },
                    { "delayBefore", delayBefore }
                }),
                timeoutMs);

            var operationXml = SelectorXmlSerializer.ToOperationXml(scope);
            return new BwElement(_rpc, _instanceId, tab.TabId, operationXml, index);
        }

        public BwElement[] FindElements(string selectorXml, BwTab tab = null)
        {
            var scope = SelectorXmlSerializer.SplitScope(selectorXml);
            if (scope.TabLevel != null)
            {
                throw new InvalidOperationException(
                    "Selector contains <wnd>. Use BridgeHost.FindElements().");
            }

            return FindElements(scope, tab);
        }

        public BwElement[] FindElements(BridgeExecutionContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (!string.Equals(context.InstanceId, _instanceId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("BridgeExecutionContext instanceId does not match this client.");

            return FindElements(context.Scope, context.Tab);
        }

        public BwElement[] FindElements(SelectorScope scope, BwTab tab)
        {
            if (tab == null)
                throw new ArgumentNullException(nameof(tab));

            var response = Invoke(
                "tab.findElements",
                BridgeRpcHost.WithScopeForTab(scope, tab, new Dictionary<string, object>()),
                15000);

            var operationXml = SelectorXmlSerializer.ToOperationXml(scope);
            return BuildElementArray(operationXml, tab.TabId, BridgeJson.GetInt(response.Data, "count"));
        }

        private BwElement[] BuildElementArray(string selectorXml, int tabId, int count)
        {
            if (count <= 0)
                return new BwElement[0];

            var result = new BwElement[count];
            for (var i = 0; i < count; i++)
                result[i] = new BwElement(_rpc, _instanceId, tabId, selectorXml, i);

            return result;
        }

        public void InputElement(
            string selectorXml,
            string value,
            BwTab tab = null,
            BridgeInputMethod inputMethod = BridgeInputMethod.Fill,
            int timeoutMs = 15000)
        {
            var scope = SelectorXmlSerializer.SplitScope(selectorXml);
            if (scope.TabLevel != null)
            {
                throw new InvalidOperationException(
                    "Selector contains <wnd>. Use BridgeHost.InputElement().");
            }

            InputElement(scope, tab, value, inputMethod, timeoutMs);
        }

        public void InputElement(
            BridgeExecutionContext context,
            string value,
            BridgeInputMethod inputMethod = BridgeInputMethod.Fill,
            int timeoutMs = 15000)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (!string.Equals(context.InstanceId, _instanceId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("BridgeExecutionContext instanceId does not match this client.");

            InputElement(context.Scope, context.Tab, value, inputMethod, timeoutMs);
        }

        public void InputElement(
            SelectorScope scope,
            BwTab tab,
            string value,
            BridgeInputMethod inputMethod = BridgeInputMethod.Fill,
            int timeoutMs = 15000)
        {
            Invoke(
                "element.input",
                BridgeRpcHost.WithScopeForTab(scope, tab, new Dictionary<string, object>
                {
                    { "value", value ?? string.Empty },
                    { "inputMethod", inputMethod.ToString() },
                    { "timeout", timeoutMs }
                }),
                timeoutMs);
        }

        public string GetInputValue(string selectorXml, BwTab tab = null, int timeoutMs = 15000)
        {
            var scope = SelectorXmlSerializer.SplitScope(selectorXml);
            if (scope.TabLevel != null)
            {
                throw new InvalidOperationException(
                    "Selector contains <wnd>. Use BridgeHost.GetInputValue().");
            }

            return GetInputValue(scope, tab, timeoutMs);
        }

        public string GetInputValue(BridgeExecutionContext context, int timeoutMs = 15000)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (!string.Equals(context.InstanceId, _instanceId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("BridgeExecutionContext instanceId does not match this client.");

            return GetInputValue(context.Scope, context.Tab, timeoutMs);
        }

        public string GetInputValue(SelectorScope scope, BwTab tab, int timeoutMs = 15000)
        {
            var response = Invoke(
                "element.getInputValue",
                BridgeRpcHost.WithScopeForTab(scope, tab, new Dictionary<string, object>
                {
                    { "timeout", timeoutMs }
                }),
                timeoutMs);

            return BridgeJson.GetString(response.Data, "value");
        }

        public string GetAttribute(string selectorXml, string name, BwTab tab = null, int timeoutMs = 15000)
        {
            var scope = SelectorXmlSerializer.SplitScope(selectorXml);
            if (scope.TabLevel != null)
            {
                throw new InvalidOperationException(
                    "Selector contains <wnd>. Use BridgeHost.GetAttribute().");
            }

            return GetAttribute(scope, tab, name, timeoutMs);
        }

        public string GetAttribute(BridgeExecutionContext context, string name, int timeoutMs = 15000)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (!string.Equals(context.InstanceId, _instanceId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("BridgeExecutionContext instanceId does not match this client.");

            return GetAttribute(context.Scope, context.Tab, name, timeoutMs);
        }

        public string GetAttribute(SelectorScope scope, BwTab tab, string name, int timeoutMs = 15000)
        {
            var response = Invoke(
                "element.getAttribute",
                BridgeRpcHost.WithScopeForTab(scope, tab, new Dictionary<string, object>
                {
                    { "name", name },
                    { "timeout", timeoutMs }
                }),
                timeoutMs);

            return BridgeJson.GetString(response.Data, "value");
        }

        private BridgeRpcResponse Invoke(string action, IDictionary<string, object> parameters, int timeoutMs)
        {
            var response = _rpc.InvokeAsync(action, _instanceId, parameters, timeoutMs).GetAwaiter().GetResult();
            BridgeClientErrors.EnsureSuccess(response, action);
            return response;
        }

        public void Dispose()
        {
            if (_ownsRpc)
                _rpc.Dispose();
        }
    }

    public sealed class BwBrowser
    {
        private readonly IBridgeRpcChannel _rpc;
        private readonly string _instanceId;

        internal BwBrowser(IBridgeRpcChannel rpc, string instanceId)
        {
            _rpc = rpc;
            _instanceId = instanceId;
        }

        public string InstanceId
        {
            get { return _instanceId; }
        }

        public int WindowId { get; private set; }

        internal void BindWindow(int windowId)
        {
            if (windowId > 0)
                WindowId = windowId;
        }

        public BwBrowser BrowserOpen(out BwTab initialTab, string url = null, int timeoutMs = 15000)
        {
            var response = Invoke("browser.open", new Dictionary<string, object>
            {
                { "url", url ?? string.Empty },
                { "timeout", timeoutMs }
            }, timeoutMs);

            WindowId = BridgeJson.GetInt(response.Data, "windowId");
            initialTab = CreateTab(response);
            return this;
        }

        public void BrowserClose()
        {
            if (WindowId <= 0)
            {
                var windowIds = CollectWindowIds();
                if (windowIds.Count == 1)
                    WindowId = windowIds.First();
                else if (windowIds.Count > 1)
                    throw new InvalidOperationException(
                        "Multiple browser windows are associated with this browser instance. " +
                        "Attach Browser must match a single window, or call BrowserOpen() first.");
            }

            if (WindowId <= 0)
                throw new InvalidOperationException(
                    "No browser window is associated with this browser instance. Call BrowserOpen() or Attach Browser first.");

            Invoke("browser.close", new Dictionary<string, object>
            {
                { "windowId", WindowId }
            }, 15000);

            WindowId = 0;
        }

        public BwTab NewTab(string url = null, int timeoutMs = 15000)
        {
            var response = Invoke("browser.newTab", new Dictionary<string, object>
            {
                { "url", url ?? string.Empty },
                { "timeout", timeoutMs }
            }, timeoutMs);

            return CreateTab(response);
        }

        public BwTab[] GetAllTabs()
        {
            var response = Invoke("browser.getAllTabs", null, 15000);
            return ParseTabs(_rpc, _instanceId, response);
        }

        public HashSet<int> CollectWindowIds()
        {
            var windowIds = new HashSet<int>();
            foreach (var tab in GetAllTabs())
            {
                if (tab.WindowId > 0)
                    windowIds.Add(tab.WindowId);
            }

            return windowIds;
        }

        /// <summary>
        /// Resolves the tab in the newly opened browser window. Does not wait for page load.
        /// </summary>
        public BwTab ResolveNewWindowTab(HashSet<int> knownWindowIds, int timeoutMs = 5000)
        {
            var response = Invoke("browser.resolveNewWindowTab", new Dictionary<string, object>
            {
                { "knownWindowIds", knownWindowIds != null ? knownWindowIds.ToArray() : new int[0] },
                { "timeout", timeoutMs }
            }, timeoutMs + 5000);

            WindowId = BridgeJson.GetInt(response.Data, "windowId");
            return CreateTab(response);
        }

        public BwTab WaitForTabByUrl(string url, int timeoutMs = 15000)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("url is required.", nameof(url));

            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                foreach (var tab in GetAllTabs())
                {
                    if (!BridgeUrlMatcher.Matches(tab.Url, url))
                        continue;

                    if (tab.WindowId > 0)
                        BindWindow(tab.WindowId);

                    return tab;
                }

                Thread.Sleep(200);
            }

            throw new TimeoutException("Tab with expected URL was not found within " + timeoutMs + "ms: " + url);
        }

        public BwTab GetActivatedTab()
        {
            var response = Invoke("browser.getActivatedTab", null, 15000);
            return CreateTab(response);
        }

        public BwBrowserStatus GetStatus()
        {
            var response = Invoke("browser.getStatus", new Dictionary<string, object>
            {
                { "windowId", WindowId }
            }, 15000);

            return BridgeTabStatusParser.ParseBrowserStatus(_rpc, _instanceId, this, response);
        }

        public BwTab GetLatestTab()
        {
            var response = Invoke("browser.getLatestTab", null, 15000);
            return CreateTab(response);
        }

        public BwTab SwitchTab(int index)
        {
            var response = Invoke("browser.switchTab", new Dictionary<string, object>
            {
                { "index", index }
            }, 15000);

            return CreateTab(response);
        }

        public BwTab SwitchTab(
            int? index = null,
            string title = null,
            string titleRe = null,
            string url = null,
            string urlRe = null,
            BwTab tab = null)
        {
            var response = Invoke("browser.switchTab", new Dictionary<string, object>
            {
                { "index", index.HasValue ? (object)index.Value : null },
                { "title", title },
                { "titleRe", titleRe },
                { "url", url },
                { "urlRe", urlRe },
                { "tabId", tab?.TabId }
            }, 15000);

            return CreateTab(response);
        }

        public BwCookie[] GetCookies(BwTab tab = null)
        {
            var response = Invoke("browser.getCookies", new Dictionary<string, object>
            {
                { "tabId", tab?.TabId }
            }, 15000);

            return ParseCookies(response);
        }

        public Dictionary<string, string> GetLocalStorage(BwTab tab = null)
        {
            return GetStorage("local", tab);
        }

        public Dictionary<string, string> GetSessionStorage(BwTab tab = null)
        {
            return GetStorage("session", tab);
        }

        private Dictionary<string, string> GetStorage(string scope, BwTab tab)
        {
            var response = Invoke("browser.getStorage", new Dictionary<string, object>
            {
                { "scope", scope },
                { "tabId", tab?.TabId }
            }, 15000);

            return ParseStringDictionary(response, "items");
        }

        private BwTab CreateTab(BridgeRpcResponse response)
        {
            return new BwTab(_rpc, _instanceId, BridgeJson.GetInt(response.Data, "tabId"));
        }

        private static BwTab[] ParseTabs(IBridgeRpcChannel rpc, string instanceId, BridgeRpcResponse response)
        {
            var items = BridgeJson.GetArray(response.Data, "tabs");
            var tabs = new List<BwTab>();
            foreach (var item in items)
            {
                var tabData = item as Dictionary<string, object>;
                if (tabData == null)
                    continue;

                tabs.Add(new BwTab(rpc, instanceId, BridgeJson.GetInt(tabData, "tabId"))
                {
                    WindowId = BridgeJson.GetInt(tabData, "windowId"),
                    Url = BridgeJson.GetString(tabData, "url"),
                    Title = BridgeJson.GetString(tabData, "title"),
                    Active = BridgeJson.GetBool(tabData, "active"),
                    Index = BridgeJson.GetInt(tabData, "index")
                });
            }

            return tabs.ToArray();
        }

        private static BwCookie[] ParseCookies(BridgeRpcResponse response)
        {
            var items = BridgeJson.GetArray(response.Data, "cookies");
            return items
                .OfType<Dictionary<string, object>>()
                .Select(item => new BwCookie
                {
                    Name = BridgeJson.GetString(item, "name"),
                    Value = BridgeJson.GetString(item, "value"),
                    Domain = BridgeJson.GetString(item, "domain"),
                    Path = BridgeJson.GetString(item, "path")
                })
                .ToArray();
        }

        private static Dictionary<string, string> ParseStringDictionary(BridgeRpcResponse response, string key)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var items = BridgeJson.GetObject(response.Data, key);
            foreach (var pair in items)
                result[pair.Key] = Convert.ToString(pair.Value);

            return result;
        }

        private BridgeRpcResponse Invoke(string action, IDictionary<string, object> parameters, int timeoutMs)
        {
            var response = _rpc.InvokeAsync(action, _instanceId, parameters, timeoutMs).GetAwaiter().GetResult();
            BridgeClientErrors.EnsureSuccess(response, action);
            return response;
        }
    }

    public sealed class BwTab
    {
        private readonly IBridgeRpcChannel _rpc;
        private readonly string _instanceId;

        internal BwTab(IBridgeRpcChannel rpc, string instanceId, int tabId)
        {
            _rpc = rpc;
            _instanceId = instanceId;
            InstanceId = instanceId;
            TabId = tabId;
        }

        public string InstanceId { get; }

        public int TabId { get; }
        public int WindowId { get; set; }
        public string Url { get; set; }
        public string Title { get; set; }
        public bool Active { get; set; }
        public int Index { get; set; }

        public BwTabInfo GetInfo()
        {
            var response = Invoke("tab.getInfo", WithTab(), 15000);
            var info = new BwTabInfo();
            BridgeTabStatusParser.ApplyTabSnapshot(response.Data, this, info);
            return info;
        }

        public void NavigateUrl(string url, bool waitForLoad = false, int timeoutMs = 15000)
        {
            var rpcTimeoutMs = waitForLoad ? timeoutMs : 5000;
            Invoke("tab.navigate", WithTab(new Dictionary<string, object>
            {
                { "url", url },
                { "waitForLoad", waitForLoad },
                { "timeout", waitForLoad ? timeoutMs : 0 }
            }), rpcTimeoutMs);
        }

        public void Back()
        {
            Invoke("tab.back", WithTab(), 15000);
        }

        public void Forward()
        {
            Invoke("tab.forward", WithTab(), 15000);
        }

        public void Refresh()
        {
            Invoke("tab.refresh", WithTab(), 15000);
        }

        public void Close()
        {
            Invoke("tab.close", WithTab(), 15000);
        }

        public void Activate()
        {
            Invoke("tab.activate", WithTab(), 5000);
        }

        public bool ElementExists(string selectorXml, int index = 0)
        {
            var response = Invoke("tab.elementExists", WithSelector(selectorXml, new Dictionary<string, object>
            {
                { "index", index }
            }), 15000);

            return BridgeJson.GetBool(response.Data, "exists");
        }

        public BwElement FindElement(
            string selectorXml,
            int index = 0,
            int timeoutMs = 15000,
            BridgeFindElementWaitState waitState = BridgeFindElementWaitState.Attached,
            int delayBefore = 300)
        {
            Invoke("element.find", WithSelector(selectorXml, new Dictionary<string, object>
            {
                { "index", index },
                { "timeout", timeoutMs },
                { "waitState", waitState.ToString() },
                { "delayBefore", delayBefore }
            }), timeoutMs);

            return new BwElement(_rpc, _instanceId, TabId, selectorXml, index);
        }

        public BwElement[] FindElements(string selectorXml)
        {
            var response = Invoke("tab.findElements", WithSelector(selectorXml, new Dictionary<string, object>()), 15000);
            return BuildElementArray(selectorXml, TabId, BridgeJson.GetInt(response.Data, "count"));
        }

        private BwElement[] BuildElementArray(string selectorXml, int tabId, int count)
        {
            if (count <= 0)
                return new BwElement[0];

            var result = new BwElement[count];
            for (var i = 0; i < count; i++)
                result[i] = new BwElement(_rpc, _instanceId, tabId, selectorXml, i);

            return result;
        }

        public int ParallelFindElement(
            IEnumerable<string> selectorXmlList,
            int timeoutMs = 15000,
            BridgeFindElementWaitState waitState = BridgeFindElementWaitState.Attached)
        {
            var selectorSets = (selectorXmlList ?? Enumerable.Empty<string>())
                .Select(selectorXml => BridgeRpcHost.BuildSelectorLevelsPayload(
                    SelectorXmlSerializer.SplitScope(selectorXml).ElementLevels))
                .ToArray();

            var response = Invoke("tab.parallelFindElement", WithTab(new Dictionary<string, object>
            {
                { "selectorSets", selectorSets },
                { "timeout", timeoutMs },
                { "waitState", waitState.ToString() }
            }), timeoutMs);

            return BridgeJson.GetInt(response.Data, "matchedIndex", -1);
        }

        public object RunJs(string script, object arg = null, int timeoutMs = 15000, bool isAsync = false)
        {
            var response = Invoke("tab.runJs", WithTab(new Dictionary<string, object>
            {
                { "script", script },
                { "arg", arg },
                { "timeout", timeoutMs },
                { "isAsync", isAsync }
            }), isAsync ? 5000 : timeoutMs);

            return response.Data.ContainsKey("result") ? response.Data["result"] : null;
        }

        public void SendKeys(string keys, int? delay = null)
        {
            Invoke("tab.sendKeys", WithTab(new Dictionary<string, object>
            {
                { "keys", keys },
                { "delay", delay }
            }), 15000);
        }

        public void TakeScreenshot(string path, bool fullPage = true, int timeoutMs = 15000)
        {
            var response = Invoke("tab.takeScreenshot", WithTab(new Dictionary<string, object>
            {
                { "path", path },
                { "fullPage", fullPage },
                { "timeout", timeoutMs }
            }), timeoutMs);

            if (!string.IsNullOrWhiteSpace(path))
            {
                BridgeScreenshotHelper.SaveDataUrl(BridgeJson.GetString(response.Data, "dataUrl"), path);
            }
        }

        public BwCookie[] GetCookies()
        {
            var response = Invoke("tab.getCookies", WithTab(), 15000);
            return ParseCookies(response);
        }

        public Dictionary<string, string> GetLocalStorage()
        {
            return GetStorage("local");
        }

        public Dictionary<string, string> GetSessionStorage()
        {
            return GetStorage("session");
        }

        private Dictionary<string, string> GetStorage(string scope)
        {
            var response = Invoke("tab.getStorage", WithTab(new Dictionary<string, object>
            {
                { "scope", scope }
            }), 15000);

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var items = BridgeJson.GetObject(response.Data, "items");
            foreach (var pair in items)
                result[pair.Key] = Convert.ToString(pair.Value);

            return result;
        }

        public BridgeInspectorPickResult InspectorStartPick(int timeoutMs = 300000)
        {
            Activate();
            var response = Invoke(
                "inspector.startPick",
                WithTab(new Dictionary<string, object> { { "timeout", timeoutMs } }),
                timeoutMs + 10000);

            return ParsePickResponse(response);
        }

        public void InspectorStartPickAssist()
        {
            Invoke("inspector.startPickAssist", WithTab(), 15000);
        }

        public void InspectorStopPickAssist()
        {
            Invoke("inspector.stopPickAssist", WithTab(), 5000);
        }

        public void InspectorPausePickAssist()
        {
            Invoke("inspector.pausePickAssist", WithTab(), 5000);
        }

        public void InspectorResumePickAssist()
        {
            Invoke("inspector.resumePickAssist", WithTab(), 5000);
        }

        public void InspectorRestartPickAssist()
        {
            Invoke("inspector.restartPickAssist", WithTab(), 15000);
        }

        public void InspectorUpdateHoverAtScreenPoint(int screenX, int screenY)
        {
            Invoke(
                "inspector.hoverAtScreenPoint",
                WithTab(new Dictionary<string, object>
                {
                    { "screenX", screenX },
                    { "screenY", screenY }
                }),
                1500);
        }

        public BridgeInspectorHoverResult InspectorHoverAtScreenPoint(int screenX, int screenY)
        {
            var response = Invoke(
                "inspector.hoverAtScreenPoint",
                WithTab(new Dictionary<string, object>
                {
                    { "screenX", screenX },
                    { "screenY", screenY }
                }),
                5000);

            return ParseHoverResponse(response);
        }

        private static BridgeInspectorHoverResult ParseHoverResponse(BridgeRpcResponse response)
        {
            var data = response.Data;
            var hovered = BridgeJson.GetBool(data, "hovered");
            if (!hovered)
            {
                return new BridgeInspectorHoverResult { Hovered = false };
            }

            var bounds = BridgeJson.GetObject(data, "bounds");
            if (bounds.Count == 0)
            {
                return new BridgeInspectorHoverResult { Hovered = false };
            }

            return new BridgeInspectorHoverResult
            {
                Hovered = true,
                Bounds = new System.Drawing.Rectangle(
                    BridgeJson.GetInt(bounds, "x"),
                    BridgeJson.GetInt(bounds, "y"),
                    Math.Max(1, BridgeJson.GetInt(bounds, "width", 1)),
                    Math.Max(1, BridgeJson.GetInt(bounds, "height", 1)))
            };
        }

        public BridgeInspectorPickResult InspectorPickAtScreenPoint(int screenX, int screenY)
        {
            var response = Invoke(
                "inspector.pickAtScreenPoint",
                WithTab(new Dictionary<string, object>
                {
                    { "screenX", screenX },
                    { "screenY", screenY }
                }),
                15000);

            return ParsePickResponse(response);
        }

        private static BridgeInspectorPickResult ParsePickResponse(BridgeRpcResponse response)
        {
            var segments = BridgeJson.GetArray(response.Data, "segments");
            if (segments.Length == 0)
            {
                return new BridgeInspectorPickResult
                {
                    Cancelled = true,
                    RestrictedUrl = string.Equals(
                        BridgeJson.GetString(response.Data, "reason"),
                        "restricted-url",
                        StringComparison.OrdinalIgnoreCase)
                };
            }

            return new BridgeInspectorPickResult
            {
                Segments = segments,
                Levels = BridgeInspectorParser.ParseLevels(BridgeJson.GetArray(response.Data, "levels")),
                DisplayName = BridgeJson.GetString(response.Data, "displayName"),
                TabTitle = BridgeJson.GetString(response.Data, "tabTitle"),
                TabUrl = BridgeJson.GetString(response.Data, "tabUrl"),
                Cancelled = false
            };
        }

        public void InspectorStopPick()
        {
            Invoke("inspector.stopPick", WithTab(), 5000);
        }

        public void InspectorPausePick()
        {
            Invoke("inspector.pausePick", WithTab(), 5000);
        }

        public void InspectorResumePick()
        {
            Invoke("inspector.resumePick", WithTab(), 5000);
        }

        public BridgeInspectorBuildResult InspectorBuildSelector(object[] segments)
        {
            var response = Invoke(
                "inspector.buildSelector",
                WithTab(new Dictionary<string, object> { { "segments", segments ?? new object[0] } }),
                30000);

            return new BridgeInspectorBuildResult
            {
                Levels = BridgeInspectorParser.ParseLevels(BridgeJson.GetArray(response.Data, "levels")),
                MinimalLevels = BridgeInspectorParser.ParseLevels(BridgeJson.GetArray(response.Data, "minimalLevels")),
                Segments = BridgeJson.GetArray(response.Data, "segments"),
                DisplayName = BridgeJson.GetString(response.Data, "displayName")
            };
        }

        public BridgeInspectorDescribeResult InspectorDescribe(object[] segments)
        {
            var response = Invoke(
                "inspector.describe",
                WithTab(new Dictionary<string, object> { { "segments", segments ?? new object[0] } }),
                15000);

            return new BridgeInspectorDescribeResult
            {
                Properties = BridgeInspectorParser.ParseProperties(BridgeJson.GetArray(response.Data, "properties")),
                DisplayName = BridgeJson.GetString(response.Data, "displayName")
            };
        }

        public void InspectorHighlight(object[] segments, int durationMs = 3000)
        {
            Activate();
            Invoke(
                "inspector.highlight",
                WithTab(new Dictionary<string, object>
                {
                    { "segments", segments ?? new object[0] },
                    { "durationMs", durationMs }
                }),
                15000);
        }

        public void InspectorHighlight(string selectorXml, int durationMs = 3000)
        {
            Activate();
            var payload = WithSelector(selectorXml, new Dictionary<string, object> { { "durationMs", durationMs } });
            Invoke("inspector.highlight", payload, 15000);
        }

        public BridgeInspectorDomNode[] InspectorGetDomChildren(object[] segments, int maxChildren = 500)
        {
            var response = Invoke(
                "inspector.getDomChildren",
                WithTab(new Dictionary<string, object>
                {
                    { "segments", segments ?? new object[0] },
                    { "maxChildren", maxChildren }
                }),
                15000);

            return BridgeInspectorParser.ParseDomNodes(BridgeJson.GetArray(response.Data, "nodes"));
        }

        public BridgeInspectorValidateProbeResult InspectorValidateProbe(
            object[] segments,
            SelectorScope scope)
        {
            var payload = WithTab(new Dictionary<string, object>
            {
                { "segments", segments ?? new object[0] }
            });

            if (scope != null)
            {
                payload["frameSelectorLevels"] = BridgeRpcHost.BuildSelectorLevelsPayload(scope.FrameLevels);
                payload["selectorLevels"] = BridgeRpcHost.BuildSelectorLevelsPayload(scope.ElementLevels);
            }

            var response = Invoke("inspector.validateProbe", payload, 15000);
            return BridgeInspectorParser.ParseValidateProbe(response.Data);
        }

        private Dictionary<string, object> WithTab(IDictionary<string, object> extra = null)
        {
            var payload = extra != null
                ? new Dictionary<string, object>(extra)
                : new Dictionary<string, object>();

            payload["tabId"] = TabId;
            return payload;
        }

        private Dictionary<string, object> WithSelector(string selectorXml, IDictionary<string, object> extra = null)
        {
            var payload = BridgeRpcHost.WithSelectorXml(selectorXml, extra);
            payload["tabId"] = TabId;
            return payload;
        }

        private static BwCookie[] ParseCookies(BridgeRpcResponse response)
        {
            var items = BridgeJson.GetArray(response.Data, "cookies");
            return items
                .OfType<Dictionary<string, object>>()
                .Select(item => new BwCookie
                {
                    Name = BridgeJson.GetString(item, "name"),
                    Value = BridgeJson.GetString(item, "value"),
                    Domain = BridgeJson.GetString(item, "domain"),
                    Path = BridgeJson.GetString(item, "path")
                })
                .ToArray();
        }

        private BridgeRpcResponse Invoke(string action, IDictionary<string, object> parameters, int timeoutMs)
        {
            var response = _rpc.InvokeAsync(action, _instanceId, parameters, timeoutMs).GetAwaiter().GetResult();
            BridgeClientErrors.EnsureSuccess(response, action);
            return response;
        }
    }

    public sealed class BwElement
    {
        private readonly IBridgeRpcChannel _rpc;
        private readonly string _instanceId;
        private readonly int _tabId;
        private readonly string _selectorXml;
        private readonly int _index;

        internal BwElement(IBridgeRpcChannel rpc, string instanceId, int tabId, string selectorXml, int index)
        {
            _rpc = rpc;
            _instanceId = instanceId;
            _tabId = tabId;
            _selectorXml = selectorXml ?? string.Empty;
            _index = index;
        }

        public void Click(
            string button = "left",
            int count = 1,
            int interval = 0,
            string[] modifiers = null,
            bool force = false,
            BridgeClickValidateMode validate = BridgeClickValidateMode.None,
            string validationSelectorXml = null,
            int waitBeforeValidate = 1000,
            BridgeClickMethod clickMethod = BridgeClickMethod.Javascript,
            int timeoutMs = 15000)
        {
            Invoke("element.click", WithSelector(new Dictionary<string, object>
            {
                { "button", button },
                { "count", count },
                { "interval", interval },
                { "modifiers", modifiers ?? new string[0] },
                { "force", force },
                { "validate", validate.ToString() },
                { "validationSelectorLevels", BuildValidationLevels(validationSelectorXml) },
                { "waitBeforeValidate", waitBeforeValidate },
                { "clickMethod", clickMethod.ToString() },
                { "timeout", timeoutMs }
            }), timeoutMs);
        }

        public void DoubleClick(
            string button = "left",
            int count = 1,
            int interval = 0,
            string[] modifiers = null,
            bool force = false,
            BridgeClickValidateMode validate = BridgeClickValidateMode.None,
            string validationSelectorXml = null,
            int waitBeforeValidate = 1000,
            BridgeClickMethod clickMethod = BridgeClickMethod.Javascript,
            int timeoutMs = 15000)
        {
            Invoke("element.doubleClick", WithSelector(new Dictionary<string, object>
            {
                { "button", button },
                { "count", count },
                { "interval", interval },
                { "modifiers", modifiers ?? new string[0] },
                { "force", force },
                { "validate", validate.ToString() },
                { "validationSelectorLevels", BuildValidationLevels(validationSelectorXml) },
                { "waitBeforeValidate", waitBeforeValidate },
                { "clickMethod", clickMethod.ToString() },
                { "timeout", timeoutMs }
            }), timeoutMs);
        }

        public BwTab ClickForNewTab(BridgeClickMethod clickMethod = BridgeClickMethod.Javascript, int timeoutMs = 15000)
        {
            var response = Invoke("element.clickForNewTab", WithSelector(new Dictionary<string, object>
            {
                { "clickMethod", clickMethod.ToString() },
                { "timeout", timeoutMs }
            }), timeoutMs);

            return new BwTab(_rpc, _instanceId, BridgeJson.GetInt(response.Data, "tabId"));
        }

        public BwDownloadInfo ClickForDownload(string saveAsPath, BridgeClickMethod clickMethod = BridgeClickMethod.Javascript, int timeoutMs = 60000)
        {
            var response = Invoke("element.clickForDownload", WithSelector(new Dictionary<string, object>
            {
                { "saveAsPath", saveAsPath },
                { "clickMethod", clickMethod.ToString() },
                { "timeout", timeoutMs }
            }), timeoutMs);

            var fileBase64 = BridgeJson.GetString(response.Data, "fileBase64");
            if (!string.IsNullOrEmpty(saveAsPath) && !string.IsNullOrEmpty(fileBase64))
            {
                var directory = Path.GetDirectoryName(saveAsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllBytes(saveAsPath, Convert.FromBase64String(fileBase64));
            }

            return new BwDownloadInfo
            {
                Url = BridgeJson.GetString(response.Data, "url"),
                SuggestedFileName = BridgeJson.GetString(response.Data, "suggestedFileName"),
                SavedPath = BridgeJson.GetString(response.Data, "savedPath")
            };
        }

        public void Input(
            string value,
            BridgeInputMethod inputMethod = BridgeInputMethod.Fill,
            float? typeDelay = null,
            bool validateContentAfterInputted = false,
            int interval = 0,
            int timeoutMs = 15000)
        {
            Invoke("element.input", WithSelector(new Dictionary<string, object>
            {
                { "value", value ?? string.Empty },
                { "inputMethod", inputMethod.ToString() },
                { "typeDelay", typeDelay },
                { "validateContentAfterInputted", validateContentAfterInputted },
                { "interval", interval },
                { "timeout", timeoutMs }
            }), timeoutMs);
        }

        public void Select(
            BridgeSelectValueType valType,
            string[] texts = null,
            string[] values = null,
            int[] indices = null,
            bool validateContentAfterSelected = false,
            int interval = 0,
            int timeoutMs = 15000)
        {
            Invoke("element.select", WithSelector(new Dictionary<string, object>
            {
                { "valType", valType.ToString() },
                { "texts", texts ?? new string[0] },
                { "values", values ?? new string[0] },
                { "indices", indices ?? new int[0] },
                { "validateContentAfterSelected", validateContentAfterSelected },
                { "interval", interval },
                { "timeout", timeoutMs }
            }), timeoutMs);
        }

        public void Check(int timeoutMs = 15000)
        {
            Invoke("element.check", WithSelector(new Dictionary<string, object> { { "timeout", timeoutMs } }), timeoutMs);
        }

        public void Uncheck(int timeoutMs = 15000)
        {
            Invoke("element.uncheck", WithSelector(new Dictionary<string, object> { { "timeout", timeoutMs } }), timeoutMs);
        }

        public bool IsChecked(int timeoutMs = 15000)
        {
            var response = Invoke("element.isChecked", WithSelector(new Dictionary<string, object> { { "timeout", timeoutMs } }), timeoutMs);
            return BridgeJson.GetBool(response.Data, "checked");
        }

        public string GetText(int timeoutMs = 15000)
        {
            var response = Invoke("element.getText", WithSelector(new Dictionary<string, object> { { "timeout", timeoutMs } }), timeoutMs);
            return BridgeJson.GetString(response.Data, "text");
        }

        public string GetAttribute(string name, int timeoutMs = 15000)
        {
            var response = Invoke("element.getAttribute", WithSelector(new Dictionary<string, object>
            {
                { "name", name },
                { "timeout", timeoutMs }
            }), timeoutMs);

            return BridgeJson.GetString(response.Data, "value");
        }

        public string GetInputValue(int timeoutMs = 15000)
        {
            var response = Invoke("element.getInputValue", WithSelector(new Dictionary<string, object> { { "timeout", timeoutMs } }), timeoutMs);
            return BridgeJson.GetString(response.Data, "value");
        }

        public string[] GetSelected(int timeoutMs = 15000)
        {
            var response = Invoke("element.getSelected", WithSelector(new Dictionary<string, object> { { "timeout", timeoutMs } }), timeoutMs);
            return BridgeJson.GetArray(response.Data, "selected").Select(Convert.ToString).ToArray();
        }

        public BwRect GetRect(int timeoutMs = 15000)
        {
            var response = Invoke("element.getRect", WithSelector(new Dictionary<string, object> { { "timeout", timeoutMs } }), timeoutMs);
            return new BwRect
            {
                X = BridgeJson.GetDouble(response.Data, "x"),
                Y = BridgeJson.GetDouble(response.Data, "y"),
                Width = BridgeJson.GetDouble(response.Data, "width"),
                Height = BridgeJson.GetDouble(response.Data, "height")
            };
        }

        public BwElement GetParent(int level = 1, int timeoutMs = 15000)
        {
            Invoke("element.getParent", WithSelector(new Dictionary<string, object>
            {
                { "level", level },
                { "timeout", timeoutMs }
            }), timeoutMs);

            return new BwElement(_rpc, _instanceId, _tabId, _selectorXml + "\n<ctrl role='parent' />", _index);
        }

        public BwElement[] GetChildren(string childSelectorXml, bool deepdive = false, int timeoutMs = 15000)
        {
            var response = Invoke("element.getChildren", WithSelector(new Dictionary<string, object>
            {
                { "childSelectorLevels", BridgeRpcHost.BuildSelectorLevelsPayload(SelectorXmlSerializer.Deserialize(childSelectorXml)) },
                { "deepdive", deepdive },
                { "timeout", timeoutMs }
            }), timeoutMs);

            var count = BridgeJson.GetInt(response.Data, "count");
            var children = new List<BwElement>();
            for (var i = 0; i < count; i++)
            {
                children.Add(new BwElement(_rpc, _instanceId, _tabId, childSelectorXml, i));
            }

            return children.ToArray();
        }

        public void SetAttribute(string name, string value, int timeoutMs = 15000)
        {
            Invoke("element.setAttribute", WithSelector(new Dictionary<string, object>
            {
                { "name", name },
                { "value", value },
                { "timeout", timeoutMs }
            }), timeoutMs);
        }

        public object RunJs(string script, object arg = null, int timeoutMs = 15000, bool isAsync = false)
        {
            var response = Invoke("element.runJs", WithSelector(new Dictionary<string, object>
            {
                { "script", script },
                { "arg", arg },
                { "timeout", timeoutMs },
                { "isAsync", isAsync }
            }), isAsync ? 5000 : timeoutMs);

            return response.Data.ContainsKey("result") ? response.Data["result"] : null;
        }

        public void SendKeys(string keys, int? delay = null, int timeoutMs = 15000)
        {
            Invoke("element.sendKeys", WithSelector(new Dictionary<string, object>
            {
                { "keys", keys },
                { "delay", delay },
                { "timeout", timeoutMs }
            }), timeoutMs);
        }

        public void TakeScreenshot(string path, int timeoutMs = 15000)
        {
            var response = Invoke("element.takeScreenshot", WithSelector(new Dictionary<string, object>
            {
                { "path", path },
                { "timeout", timeoutMs }
            }), timeoutMs);

            var dataUrl = BridgeJson.GetString(response.Data, "dataUrl");
            if (!string.IsNullOrWhiteSpace(path) && !string.IsNullOrWhiteSpace(dataUrl))
                BridgeScreenshotHelper.SaveDataUrl(dataUrl, path);
        }

        public BwElement FindElement(
            string selectorXml,
            int index = 0,
            int timeoutMs = 15000,
            BridgeFindElementWaitState waitState = BridgeFindElementWaitState.Attached,
            int delayBefore = 300)
        {
            Invoke("element.findScoped", WithSelector(new Dictionary<string, object>
            {
                { "scopedSelectorLevels", BridgeRpcHost.BuildSelectorLevelsPayload(SelectorXmlSerializer.Deserialize(selectorXml)) },
                { "index", index },
                { "timeout", timeoutMs },
                { "waitState", waitState.ToString() },
                { "delayBefore", delayBefore }
            }), timeoutMs);

            return new BwElement(_rpc, _instanceId, _tabId, _selectorXml + "\n" + selectorXml, index);
        }

        public BwElement[] FindElements(string scopedSelectorXml)
        {
            var response = Invoke("element.findElements", WithSelector(new Dictionary<string, object>
            {
                { "scopedSelectorLevels", BridgeRpcHost.BuildSelectorLevelsPayload(SelectorXmlSerializer.Deserialize(scopedSelectorXml)) }
            }), 15000);

            var combinedSelector = _selectorXml + "\n" + scopedSelectorXml;
            var count = BridgeJson.GetInt(response.Data, "count");
            if (count <= 0)
                return new BwElement[0];

            var result = new BwElement[count];
            for (var i = 0; i < count; i++)
                result[i] = new BwElement(_rpc, _instanceId, _tabId, combinedSelector, i);

            return result;
        }

        public int ParallelFindElement(
            IEnumerable<string> selectorXmlList,
            int timeoutMs = 15000,
            BridgeFindElementWaitState waitState = BridgeFindElementWaitState.Attached)
        {
            var selectorSets = (selectorXmlList ?? Enumerable.Empty<string>())
                .Select(selectorXml => BridgeRpcHost.BuildSelectorLevelsPayload(
                    SelectorXmlSerializer.SplitScope(selectorXml).ElementLevels))
                .ToArray();

            var response = Invoke("element.parallelFindElement", WithSelector(new Dictionary<string, object>
            {
                { "selectorSets", selectorSets },
                { "timeout", timeoutMs },
                { "waitState", waitState.ToString() }
            }), timeoutMs);

            return BridgeJson.GetInt(response.Data, "matchedIndex", -1);
        }

        private object[] BuildValidationLevels(string validationSelectorXml)
        {
            if (string.IsNullOrWhiteSpace(validationSelectorXml))
                return new object[0];

            var scope = SelectorXmlSerializer.SplitScope(validationSelectorXml);
            return BridgeRpcHost.BuildSelectorLevelsPayload(scope.ElementLevels);
        }

        private Dictionary<string, object> WithSelector(IDictionary<string, object> extra)
        {
            var payload = BridgeRpcHost.WithSelectorXml(_selectorXml, extra);
            payload["tabId"] = _tabId;
            payload["index"] = _index;
            return payload;
        }

        private BridgeRpcResponse Invoke(string action, IDictionary<string, object> parameters, int timeoutMs)
        {
            var response = _rpc.InvokeAsync(action, _instanceId, parameters, timeoutMs).GetAwaiter().GetResult();
            BridgeClientErrors.EnsureSuccess(response, action);
            return response;
        }
    }

    internal static class BridgeClientErrors
    {
        public static void EnsureSuccess(BridgeRpcResponse response, string action)
        {
            if (response == null)
                throw new InvalidOperationException("Bridge returned empty response for " + action + ".");

            if (response.Success)
                return;

            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(response.Error)
                    ? "Bridge command failed: " + action
                    : response.Error);
        }
    }
}
