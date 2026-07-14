using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using F2B.Browser.Chromium.Cdp.Browser;
using F2B.Browser.Chromium.Cdp.Exceptions;
using F2B.Browser.Chromium.Cdp.Selectors;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal sealed class CdpTabSession : IDisposable
    {
        private readonly CdpTab _tab;
        private readonly object _syncRoot = new object();
        private readonly object _commandLock = new object();
        private CdpClient _client;
        private bool _initialized;
        private string _mainFrameId;
        private string _rootObjectId;
        private bool _isLoading;
        private string _readyState;
        private bool _hasAlert;

        public CdpTabSession(CdpTab tab)
        {
            _tab = tab;
        }

        public CdpTab Tab
        {
            get { return _tab; }
        }

        public CdpClient Client
        {
            get
            {
                lock (_syncRoot)
                {
                    EnsureInitialized();
                    return _client;
                }
            }
        }

        public bool HasAlert
        {
            get { return _hasAlert; }
        }

        public bool IsLoading
        {
            get { return _isLoading; }
        }

        public string ReadyState
        {
            get { return _readyState ?? "connecting"; }
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                if (_client != null)
                {
                    _client.Dispose();
                    _client = null;
                }

                _initialized = false;
            }
        }

        public Dictionary<string, object> Send(string method, Dictionary<string, object> parameters = null)
        {
            return Send(method, parameters, null);
        }

        internal Dictionary<string, object> Send(
            string method,
            Dictionary<string, object> parameters,
            TimeSpan? commandTimeout)
        {
            lock (_syncRoot)
            {
                EnsureInitialized();
            }

            return SendCore(method, parameters, commandTimeout);
        }

        private Dictionary<string, object> SendCore(
            string method,
            Dictionary<string, object> parameters,
            TimeSpan? commandTimeout)
        {
            lock (_commandLock)
            {
                return commandTimeout.HasValue
                    ? _client.Send(method, parameters, commandTimeout.Value)
                    : _client.Send(method, parameters);
            }
        }

        public CdpDomContext CreateDomContext(IList<SelectorLevel> frameLevels)
        {
            lock (_syncRoot)
            {
                EnsureInitialized();
            }

            return CdpDomContext.ForFrameLevels(_tab, this, frameLevels);
        }

        public string GetHtml()
        {
            EnsureDocumentRoot();
            var result = Send("DOM.getOuterHTML", new Dictionary<string, object>
            {
                { "objectId", _rootObjectId }
            });

            return CdpValueConverter.GetString(result, "outerHTML") ?? string.Empty;
        }

        public string GetUserAgent()
        {
            return EvaluateString("navigator.userAgent;");
        }

        public Dictionary<string, object> GetTargetInfo()
        {
            return Send("Target.getTargetInfo", new Dictionary<string, object>
            {
                { "targetId", _tab.Id }
            });
        }

        public CdpCookies GetCookies()
        {
            var url = _tab.Url;
            if (string.IsNullOrWhiteSpace(url))
            {
                return new CdpCookies(new List<CdpCookieItem>());
            }

            var result = Send("Network.getCookies", new Dictionary<string, object>
            {
                { "urls", new[] { url } }
            });

            var cookies = CdpValueConverter.GetList(result, "cookies");
            var items = new List<CdpCookieItem>();
            if (cookies != null)
            {
                foreach (var entry in cookies)
                {
                    var cookie = entry as Dictionary<string, object>;
                    if (cookie == null)
                    {
                        continue;
                    }

                    var name = CdpValueConverter.GetString(cookie, "name");
                    if (string.IsNullOrEmpty(name))
                    {
                        continue;
                    }

                    items.Add(new CdpCookieItem(name, CdpValueConverter.GetString(cookie, "value") ?? string.Empty));
                }
            }

            return new CdpCookies(items);
        }

        public Dictionary<string, string> GetSessionStorage()
        {
            return CdpValueConverter.ToStringDictionary(Evaluate(GetStorageScript("sessionStorage")));
        }

        public Dictionary<string, string> GetLocalStorage()
        {
            return CdpValueConverter.ToStringDictionary(Evaluate(GetStorageScript("localStorage")));
        }

        public CdpTabStateSnapshot GetStates()
        {
            lock (_syncRoot)
            {
                EnsureInitialized();
            }

            var snapshot = new CdpTabStateSnapshot
            {
                HasAlert = _hasAlert,
                IsLoading = _isLoading,
                ReadyState = ReadyState
            };

            try
            {
                Send("Page.getLayoutMetrics");
                snapshot.IsAlive = true;
            }
            catch (BrowserException)
            {
                snapshot.IsAlive = false;
            }

            return snapshot;
        }

        public CdpTabRectSnapshot GetRect()
        {
            var layout = Send("Page.getLayoutMetrics");
            var visual = CdpValueConverter.GetDictionary(layout, "visualViewport") ?? new Dictionary<string, object>();
            var content = CdpValueConverter.GetDictionary(layout, "contentSize") ?? new Dictionary<string, object>();
            var layoutViewport = CdpValueConverter.GetDictionary(layout, "layoutViewport") ?? new Dictionary<string, object>();

            var innerSizeText = EvaluateString("window.innerWidth + ' ' + window.innerHeight;");
            var innerParts = innerSizeText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var innerWidth = innerParts.Length > 0 ? ParseInt(innerParts[0]) : 0;
            var innerHeight = innerParts.Length > 1 ? ParseInt(innerParts[1]) : 0;

            var windowBounds = _tab.Browser.GetWindowBounds(_tab.Id);
            var windowState = CdpValueConverter.GetString(windowBounds, "windowState") ?? "normal";
            var left = CdpValueConverter.GetInt(windowBounds, "left");
            var top = CdpValueConverter.GetInt(windowBounds, "top");
            var width = CdpValueConverter.GetInt(windowBounds, "width");
            var height = CdpValueConverter.GetInt(windowBounds, "height");

            Tuple<int, int> windowLocation;
            Tuple<int, int> windowSize;
            if (string.Equals(windowState, "maximized", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(windowState, "fullscreen", StringComparison.OrdinalIgnoreCase))
            {
                windowLocation = Tuple.Create(0, 0);
            }
            else
            {
                windowLocation = Tuple.Create(left + 7, top);
            }

            if (string.Equals(windowState, "fullscreen", StringComparison.OrdinalIgnoreCase))
            {
                windowSize = Tuple.Create(width, height);
            }
            else if (string.Equals(windowState, "maximized", StringComparison.OrdinalIgnoreCase))
            {
                windowSize = Tuple.Create(width - 16, height - 16);
            }
            else
            {
                windowSize = Tuple.Create(width - 16, height - 7);
            }

            var viewportWidth = CdpValueConverter.GetInt(visual, "clientWidth");
            var viewportHeight = CdpValueConverter.GetInt(visual, "clientHeight");
            var pageX = CdpValueConverter.GetInt(visual, "pageX");
            var pageY = CdpValueConverter.GetInt(visual, "pageY");
            var layoutPageX = CdpValueConverter.GetInt(layoutViewport, "pageX");
            var layoutPageY = CdpValueConverter.GetInt(layoutViewport, "pageY");

            var viewportLocation = Tuple.Create(
                windowLocation.Item1 + windowSize.Item1 - innerWidth,
                windowLocation.Item2 + windowSize.Item2 - innerHeight);

            return new CdpTabRectSnapshot
            {
                Size = Tuple.Create(
                    (int)Math.Round(Convert.ToDouble(content.ContainsKey("width") ? content["width"] : 0)),
                    (int)Math.Round(Convert.ToDouble(content.ContainsKey("height") ? content["height"] : 0))),
                WindowSize = windowSize,
                WindowLocation = windowLocation,
                WindowState = windowState,
                ViewportSize = Tuple.Create(viewportWidth, viewportHeight),
                ViewportSizeWithScrollbar = Tuple.Create(innerWidth, innerHeight),
                PageLocation = Tuple.Create(viewportLocation.Item1 - layoutPageX, viewportLocation.Item2 - layoutPageY),
                ViewportLocation = viewportLocation,
                ScrollPosition = Tuple.Create(pageX, pageY)
            };
        }

        public CdpElement ResolveElement(string objectId)
        {
            if (string.IsNullOrEmpty(objectId))
            {
                return null;
            }

            var request = Send("DOM.requestNode", new Dictionary<string, object>
            {
                { "objectId", objectId }
            });

            var nodeId = CdpValueConverter.GetInt(request, "nodeId");
            var describe = Send("DOM.describeNode", new Dictionary<string, object>
            {
                { "nodeId", nodeId }
            });

            var node = CdpValueConverter.GetDictionary(describe, "node");
            var tag = CdpValueConverter.GetString(node, "localName") ?? string.Empty;
            var backendNodeId = CdpValueConverter.GetInt(node, "backendNodeId");

            return new CdpElement(_tab, tag, backendNodeId, nodeId, objectId);
        }

        internal string EvaluateString(string expression)
        {
            var result = Evaluate(expression);
            return result == null ? string.Empty : Convert.ToString(result);
        }

        internal object Evaluate(string expression)
        {
            var response = Send("Runtime.evaluate", new Dictionary<string, object>
            {
                { "expression", expression },
                { "returnByValue", true }
            });

            var inner = CdpValueConverter.GetDictionary(response, "result");
            if (inner == null)
            {
                return null;
            }

            object exceptionDetails;
            if (inner.TryGetValue("exceptionDetails", out exceptionDetails) && exceptionDetails != null)
            {
                throw new BrowserException(string.Format("JavaScript evaluation failed: {0}", exceptionDetails));
            }

            object value;
            return inner.TryGetValue("value", out value) ? value : null;
        }

        internal string EvaluateObjectId(string expression)
        {
            var response = Send("Runtime.evaluate", new Dictionary<string, object>
            {
                { "expression", expression },
                { "returnByValue", false }
            });

            var inner = CdpValueConverter.GetDictionary(response, "result");
            return inner != null ? CdpValueConverter.GetString(inner, "objectId") : null;
        }

        public CdpResponse Get(string url)
        {
            return SendBrowserHttp("GET", url, null);
        }

        public CdpResponse Post(string url, string data = null, Dictionary<string, object> dict = null)
        {
            var postData = ResolvePostData(data, dict);
            return SendBrowserHttp("POST", url, postData);
        }

        public object RunJs(string script, object[] args = null, bool asExpression = false, bool isAsync = false, int timeoutMs = 30000)
        {
            if (string.IsNullOrWhiteSpace(script))
            {
                throw new ArgumentNullException("script");
            }

            if (isAsync)
            {
                var scriptCopy = script;
                var argsCopy = args;
                var asExprCopy = asExpression;
                ThreadPool.QueueUserWorkItem(_ => RunJsCore(scriptCopy, argsCopy, asExprCopy, timeoutMs));
                return null;
            }

            return RunJsCore(script, args, asExpression, timeoutMs);
        }

        private object RunJsCore(string script, object[] args, bool asExpression, int timeoutMs)
        {
            if (HasAlert)
            {
                throw new BrowserException("JavaScript dialog is open.");
            }

            EnsureDocumentRoot();
            var timeout = TimeSpan.FromMilliseconds(timeoutMs <= 0 ? 30000 : timeoutMs);

            Dictionary<string, object> response;
            if (asExpression)
            {
                response = Send("Runtime.evaluate", new Dictionary<string, object>
                {
                    { "expression", script },
                    { "returnByValue", true },
                    { "awaitPromise", true },
                    { "userGesture", true }
                }, timeout);
            }
            else
            {
                response = Send("Runtime.callFunctionOn", new Dictionary<string, object>
                {
                    { "functionDeclaration", CdpJsScript.WrapAsFunction(script) },
                    { "objectId", _rootObjectId },
                    { "arguments", BuildCallFunctionArguments(args) },
                    { "returnByValue", true },
                    { "awaitPromise", true },
                    { "userGesture", true }
                }, timeout);
            }

            return ParseRuntimeResult(response);
        }

        private static IList BuildCallFunctionArguments(object[] args)
        {
            var list = new List<Dictionary<string, object>>();
            if (args == null)
            {
                return list;
            }

            foreach (var arg in args)
            {
                list.Add(new Dictionary<string, object> { { "value", arg } });
            }

            return list;
        }

        private static object ParseRuntimeResult(Dictionary<string, object> response)
        {
            if (response == null)
            {
                return null;
            }

            object exceptionDetails;
            if (response.TryGetValue("exceptionDetails", out exceptionDetails) && exceptionDetails != null)
            {
                throw new BrowserException(string.Format("JavaScript execution failed: {0}", exceptionDetails));
            }

            var inner = CdpValueConverter.GetDictionary(response, "result");
            if (inner == null)
            {
                return null;
            }

            object value;
            if (inner.TryGetValue("value", out value))
            {
                return value;
            }

            object unserializableValue;
            if (inner.TryGetValue("unserializableValue", out unserializableValue))
            {
                return unserializableValue;
            }

            if (string.Equals(CdpValueConverter.GetString(inner, "subtype"), "null", StringComparison.Ordinal))
            {
                return null;
            }

            return null;
        }

        private static string ResolvePostData(string data, Dictionary<string, object> dict)
        {
            if (dict != null && dict.Count > 0)
            {
                return FormUrlEncoder.Encode(dict);
            }

            return data ?? string.Empty;
        }

        private CdpResponse SendBrowserHttp(string method, string url, string data)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentNullException("url");
            }

            var serializer = new CdpJsonSerializer();
            var urlLiteral = serializer.Serialize(url);
            var dataLiteral = serializer.Serialize(data ?? string.Empty);
            var expression = BuildBrowserHttpScript(method, urlLiteral, dataLiteral);
            var json = Convert.ToString(RunJs(expression, asExpression: true));
            return ParseBrowserHttpResponse(json, serializer);
        }

        private static string BuildBrowserHttpScript(string method, string urlLiteral, string bodyLiteral)
        {
            if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
            {
                return string.Format(
                    @"(async function() {{
                        try {{
                            var url = {0};
                            var body = {1};
                            var response = await fetch(url, {{
                                method: 'POST',
                                headers: {{ 'Content-Type': 'application/x-www-form-urlencoded' }},
                                body: body
                            }});
                            var text = await response.text();
                            return JSON.stringify({{ status: response.status, text: text }});
                        }} catch (error) {{
                            return JSON.stringify({{ status: 0, text: String(error) }});
                        }}
                    }})();",
                    urlLiteral,
                    bodyLiteral);
            }

            return string.Format(
                @"(async function() {{
                    try {{
                        var url = {0};
                        var response = await fetch(url, {{ method: 'GET' }});
                        var text = await response.text();
                        return JSON.stringify({{ status: response.status, text: text }});
                    }} catch (error) {{
                        return JSON.stringify({{ status: 0, text: String(error) }});
                    }}
                }})();",
                urlLiteral);
        }

        private static CdpResponse ParseBrowserHttpResponse(string json, CdpJsonSerializer serializer)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new BrowserException("In-tab HTTP request returned empty response.");
            }

            var payload = serializer.DeserializeObject(json) as Dictionary<string, object>;
            if (payload == null)
            {
                throw new BrowserException(string.Format("In-tab HTTP request returned invalid JSON: {0}", json));
            }

            object statusValue;
            if (!payload.TryGetValue("status", out statusValue) || statusValue == null)
            {
                throw new BrowserException(string.Format("In-tab HTTP response missing status: {0}", json));
            }

            object textValue;
            payload.TryGetValue("text", out textValue);

            return new CdpResponse(Convert.ToInt32(statusValue), textValue == null ? string.Empty : Convert.ToString(textValue));
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_tab.WebSocketDebuggerUrl))
            {
                throw new BrowserException(string.Format("Tab {0} has no WebSocketDebuggerUrl.", _tab.Id));
            }

            _client = new CdpClient(_tab.WebSocketDebuggerUrl);
            _client.Start();
            _initialized = true;
            RegisterEventHandlers(_client);
            _client.Enable("Page", "Runtime", "DOM", "Network");

            var frameTree = SendCore("Page.getFrameTree", null, null);
            var rootFrame = CdpValueConverter.GetDictionary(CdpValueConverter.GetDictionary(frameTree, "frameTree"), "frame");
            _mainFrameId = CdpValueConverter.GetString(rootFrame, "id");

            if (TryGetJsReadyStateCore() == "complete")
            {
                EnsureDocumentRootCore();
                _readyState = "complete";
                _isLoading = false;
            }
            else
            {
                _readyState = "connecting";
                _isLoading = true;
            }
        }

        private void RegisterEventHandlers(CdpClient client)
        {
            client.SetCallback("Page.javascriptDialogOpening", _ => { _hasAlert = true; }, immediate: true);
            client.SetCallback("Page.javascriptDialogClosed", _ => { _hasAlert = false; }, immediate: true);

            client.SetCallback("Page.frameStartedLoading", parameters =>
            {
                if (IsMainFrameEvent(parameters))
                {
                    _readyState = "connecting";
                    _isLoading = true;
                }
            });

            client.SetCallback("Page.frameNavigated", parameters =>
            {
                if (IsMainFrameEvent(parameters) || TryUpdateMainFrameFromNavigated(parameters))
                {
                    _readyState = "loading";
                    _isLoading = true;
                    _rootObjectId = null;
                }
            });

            client.SetCallback("Page.domContentEventFired", _ =>
            {
                _readyState = "interactive";
            });

            client.SetCallback("Page.loadEventFired", _ =>
            {
                _readyState = "complete";
                _isLoading = false;
            });

            client.SetCallback("Page.frameStoppedLoading", parameters =>
            {
                if (IsMainFrameEvent(parameters))
                {
                    _readyState = "complete";
                    _isLoading = false;
                }
            });
        }

        private bool TryUpdateMainFrameFromNavigated(Dictionary<string, object> parameters)
        {
            var frame = CdpValueConverter.GetDictionary(parameters, "frame");
            if (frame == null)
            {
                return false;
            }

            // Top-level navigations may replace the main frame id. Keep our tracker in sync so
            // frameStoppedLoading / frameStartedLoading are not ignored after history Back/Forward.
            var parentId = CdpValueConverter.GetString(frame, "parentId");
            if (!string.IsNullOrEmpty(parentId))
            {
                return false;
            }

            var frameId = CdpValueConverter.GetString(frame, "id");
            if (string.IsNullOrEmpty(frameId))
            {
                return false;
            }

            _mainFrameId = frameId;
            return true;
        }

        private bool IsMainFrameEvent(Dictionary<string, object> parameters)
        {
            var frameId = CdpValueConverter.GetString(parameters, "frameId");
            if (!string.IsNullOrEmpty(frameId))
            {
                return frameId == _mainFrameId;
            }

            var frame = CdpValueConverter.GetDictionary(parameters, "frame");
            return frame != null && CdpValueConverter.GetString(frame, "id") == _mainFrameId;
        }

        private bool EnsureDocumentRoot()
        {
            lock (_syncRoot)
            {
                EnsureInitialized();
                return EnsureDocumentRootCore();
            }
        }

        private bool EnsureDocumentRootCore()
        {
            if (!string.IsNullOrEmpty(_rootObjectId))
            {
                return true;
            }

            try
            {
                var document = SendCore("DOM.getDocument", null, null);
                var root = CdpValueConverter.GetDictionary(document, "root");
                var backendNodeId = CdpValueConverter.GetInt(root, "backendNodeId");
                if (backendNodeId <= 0)
                {
                    return false;
                }

                var resolved = SendCore("DOM.resolveNode", new Dictionary<string, object>
                {
                    { "backendNodeId", backendNodeId }
                }, null);

                var obj = CdpValueConverter.GetDictionary(resolved, "object");
                _rootObjectId = CdpValueConverter.GetString(obj, "objectId");
                return !string.IsNullOrEmpty(_rootObjectId);
            }
            catch
            {
                return false;
            }
        }

        private string TryGetJsReadyState()
        {
            return TryGetJsReadyStateCore();
        }

        private string TryGetJsReadyStateCore()
        {
            try
            {
                var response = SendCore("Runtime.evaluate", new Dictionary<string, object>
                {
                    { "expression", "document.readyState || ''" },
                    { "returnByValue", true }
                }, null);

                var inner = CdpValueConverter.GetDictionary(response, "result");
                var value = inner != null ? CdpValueConverter.GetString(inner, "value") : null;
                return (value ?? string.Empty).ToLowerInvariant();
            }
            catch
            {
                return string.Empty;
            }
        }

        internal string TryGetMainReadyState()
        {
            lock (_syncRoot)
            {
                EnsureInitialized();
                return TryGetJsReadyStateCore();
            }
        }

        internal void ClearLoadingFlagIfDocumentComplete()
        {
            lock (_syncRoot)
            {
                if (!_initialized)
                {
                    return;
                }

                if (TryGetJsReadyStateCore() == "complete")
                {
                    _readyState = "complete";
                    _isLoading = false;
                }
            }
        }

        internal void WaitForDocumentComplete(CdpDocumentWaitScope scope, int timeoutMs)
        {
            CdpDocumentLoadWaiter.Wait(this, scope, timeoutMs);
        }

        internal void Navigate(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentNullException("url");
            }

            lock (_syncRoot)
            {
                EnsureInitialized();
                _isLoading = true;
                _readyState = "loading";
                _rootObjectId = null;
            }

            var result = Send("Page.navigate", new Dictionary<string, object>
            {
                { "url", url }
            });

            var errorText = CdpValueConverter.GetString(result, "errorText");
            if (!string.IsNullOrEmpty(errorText))
            {
                throw new BrowserException(string.Format("Navigation failed: {0}", errorText));
            }

            _tab.Browser.TryRefreshTab(_tab);

            lock (_syncRoot)
            {
                EnsureDocumentRootCore();
            }
        }

        internal void Refresh(bool ignoreCache)
        {
            lock (_syncRoot)
            {
                EnsureInitialized();
                _isLoading = true;
                _readyState = "loading";
            }

            Send("Page.reload", new Dictionary<string, object>
            {
                { "ignoreCache", ignoreCache }
            });

            _tab.Browser.TryRefreshTab(_tab);
        }

        internal void Back(int steps)
        {
            NavigateHistory(-Math.Abs(steps));
        }

        internal void Forward(int steps)
        {
            NavigateHistory(Math.Abs(steps));
        }

        private void NavigateHistory(int steps)
        {
            if (steps == 0)
            {
                return;
            }

            lock (_syncRoot)
            {
                EnsureInitialized();
            }

            var historyResult = Send("Page.getNavigationHistory");
            var currentIndex = CdpValueConverter.GetInt(historyResult, "currentIndex");
            var entries = CdpValueConverter.GetList(historyResult, "entries");
            if (entries == null || entries.Count == 0)
            {
                return;
            }

            var targetIndex = currentIndex + steps;
            if (targetIndex < 0 || targetIndex >= entries.Count)
            {
                return;
            }

            var entryId = GetHistoryEntryId(entries[targetIndex]);
            if (!entryId.HasValue)
            {
                return;
            }

            lock (_syncRoot)
            {
                _isLoading = true;
                _readyState = "loading";
                _rootObjectId = null;
            }

            Send("Page.navigateToHistoryEntry", new Dictionary<string, object>
            {
                { "entryId", entryId.Value }
            });

            _tab.Browser.TryRefreshTab(_tab);

            CdpDocumentLoadWaiter.Wait(this, CdpDocumentWaitScope.MainDocument, 15000);

            lock (_syncRoot)
            {
                _rootObjectId = null;
                EnsureDocumentRootCore();
            }
        }

        private static int? GetHistoryEntryId(object entryObj)
        {
            var entry = entryObj as Dictionary<string, object>;
            if (entry == null)
            {
                return null;
            }

            var entryId = CdpValueConverter.GetInt(entry, "id", -1);
            return entryId >= 0 ? (int?)entryId : null;
        }

        private static string GetStorageScript(string storageName)
        {
            return string.Format(
                @"(function() {{
                    var storage = window.{0};
                    if (!storage) return {{}};
                    var result = {{}};
                    for (var i = 0; i < storage.length; i++) {{
                        var key = storage.key(i);
                        result[key] = storage.getItem(key);
                    }}
                    return result;
                }})();",
                storageName);
        }

        private static int ParseInt(string value)
        {
            int parsed;
            return int.TryParse(value, out parsed) ? parsed : 0;
        }
    }
}
