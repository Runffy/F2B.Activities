using System;
using System.Collections.Generic;
using System.Threading;
using F2B.Browser.Chromium.Bridge.Selectors;

namespace F2B.Browser.Chromium.Bridge
{
    public sealed class BridgeExecutionContext
    {
        public BridgeExecutionContext(string instanceId, BwTab tab, SelectorScope scope)
        {
            InstanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));
            Tab = tab ?? throw new ArgumentNullException(nameof(tab));
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        }

        public string InstanceId { get; }

        public BwTab Tab { get; }

        public SelectorScope Scope { get; }
    }

    /// <summary>
    /// Multi-instance orchestration: resolve &lt;wnd&gt; across connected browsers, then run scoped operations.
    /// </summary>
    public sealed class BridgeHost : IDisposable
    {
        private const int DefaultWndPollIntervalMs = 300;

        private readonly BridgeWebSocketServer _server;
        private readonly IBridgeRpcChannel _attachRpc;
        private readonly Func<IReadOnlyList<BridgeClientInfo>> _listClients;
        private readonly Dictionary<string, BridgeSyncClient> _clients =
            new Dictionary<string, BridgeSyncClient>(StringComparer.OrdinalIgnoreCase);

        public BridgeHost(BridgeWebSocketServer server)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _listClients = () => _server.GetConnectedClients();
        }

        private BridgeHost(IBridgeRpcChannel attachRpc, Func<IReadOnlyList<BridgeClientInfo>> listClients)
        {
            _attachRpc = attachRpc ?? throw new ArgumentNullException(nameof(attachRpc));
            _listClients = listClients ?? throw new ArgumentNullException(nameof(listClients));
        }

        public static BridgeHost Attach(BridgeRemoteRpcHost remoteRpc)
        {
            if (remoteRpc == null)
                throw new ArgumentNullException(nameof(remoteRpc));

            return new BridgeHost(remoteRpc, remoteRpc.ListExtensionClients);
        }

        public BridgeSessionMode Mode =>
            _server != null ? BridgeSessionMode.Owner : BridgeSessionMode.Attached;

        public BridgeWebSocketServer Server
        {
            get { return _server; }
        }

        public BridgeExecutionContext ResolveContext(string selectorXml, BwTab explicitTab = null)
        {
            return ResolveContext(selectorXml, explicitTab, timeoutMs: 0);
        }

        /// <summary>
        /// Resolves selector scope. When the selector contains &lt;wnd&gt; and timeoutMs &gt; 0,
        /// polls until a matching tab appears or the timeout expires.
        /// </summary>
        public BridgeExecutionContext ResolveContext(
            string selectorXml,
            BwTab explicitTab,
            int timeoutMs,
            int pollIntervalMs = DefaultWndPollIntervalMs)
        {
            var scope = SelectorXmlSerializer.SplitScope(selectorXml);

            if (scope.TabLevel == null)
            {
                if (explicitTab == null)
                    throw new InvalidOperationException(
                        "Selector has no <wnd> level. Pass an explicit BwTab that includes browser instance and tab id.");

                return new BridgeExecutionContext(explicitTab.InstanceId, explicitTab, scope);
            }

            if (timeoutMs <= 0)
                return ResolveWndContext(scope);

            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            Exception lastError = null;

            while (true)
            {
                try
                {
                    return ResolveWndContext(scope);
                }
                catch (InvalidOperationException ex) when (IsNoTabMatchedError(ex))
                {
                    lastError = ex;
                }

                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    throw new TimeoutException(
                        "No tab matched the <wnd> selector within " + timeoutMs + " ms.",
                        lastError);
                }

                var delayMs = Math.Min(
                    pollIntervalMs,
                    Math.Max(1, (int)Math.Ceiling(remaining.TotalMilliseconds)));
                Thread.Sleep(delayMs);
            }
        }

        public string GetElementText(string selectorXml, BwTab tab = null, int timeoutMs = 15000)
        {
            var deadline = CreateDeadline(timeoutMs);
            var context = ResolveContext(selectorXml, tab, timeoutMs);
            return GetClient(context.InstanceId).GetElementText(context, RemainingMs(deadline));
        }

        public void ClickElement(string selectorXml, BwTab tab = null, int timeoutMs = 15000)
        {
            var deadline = CreateDeadline(timeoutMs);
            var context = ResolveContext(selectorXml, tab, timeoutMs);
            GetClient(context.InstanceId).ClickElement(context, RemainingMs(deadline));
        }

        public bool ElementExists(string selectorXml, BwTab tab = null, int index = 0)
        {
            var context = ResolveContext(selectorXml, tab, timeoutMs: 15000);
            return GetClient(context.InstanceId).ElementExists(context, index);
        }

        public BwElement FindElement(
            string selectorXml,
            BwTab tab = null,
            int index = 0,
            int timeoutMs = 15000,
            BridgeFindElementWaitState waitState = BridgeFindElementWaitState.Attached,
            int delayBefore = 300)
        {
            var deadline = CreateDeadline(timeoutMs);
            var context = ResolveContext(selectorXml, tab, timeoutMs);
            return GetClient(context.InstanceId).FindElement(
                context,
                index,
                RemainingMs(deadline),
                waitState,
                delayBefore);
        }

        public BwElement[] FindElements(string selectorXml, BwTab tab = null, int timeoutMs = 15000)
        {
            var context = ResolveContext(selectorXml, tab, timeoutMs);
            return GetClient(context.InstanceId).FindElements(context);
        }

        public void InputElement(
            string selectorXml,
            string value,
            BwTab tab = null,
            BridgeInputMethod inputMethod = BridgeInputMethod.Fill,
            int timeoutMs = 15000)
        {
            var deadline = CreateDeadline(timeoutMs);
            var context = ResolveContext(selectorXml, tab, timeoutMs);
            GetClient(context.InstanceId).InputElement(
                context,
                value,
                inputMethod,
                RemainingMs(deadline));
        }

        public string GetInputValue(string selectorXml, BwTab tab = null, int timeoutMs = 15000)
        {
            var deadline = CreateDeadline(timeoutMs);
            var context = ResolveContext(selectorXml, tab, timeoutMs);
            return GetClient(context.InstanceId).GetInputValue(context, RemainingMs(deadline));
        }

        public string GetAttribute(string selectorXml, string name, BwTab tab = null, int timeoutMs = 15000)
        {
            var deadline = CreateDeadline(timeoutMs);
            var context = ResolveContext(selectorXml, tab, timeoutMs);
            return GetClient(context.InstanceId).GetAttribute(context, name, RemainingMs(deadline));
        }

        public BridgeSyncClient GetClient(string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
                throw new ArgumentException("Extension instanceId is required.", nameof(instanceId));

            BridgeSyncClient client;
            if (!_clients.TryGetValue(instanceId, out client))
            {
                client = _server != null
                    ? new BridgeSyncClient(_server, instanceId)
                    : new BridgeSyncClient(_attachRpc, instanceId, false);
                _clients[instanceId] = client;
            }

            return client;
        }

        public void Dispose()
        {
            foreach (var client in _clients.Values)
                client.Dispose();

            _clients.Clear();
        }

        private BridgeExecutionContext ResolveWndContext(SelectorScope scope)
        {
            var picked = TryPickWndMatch(scope);
            if (picked == null)
                throw new InvalidOperationException("No tab matched the <wnd> selector.");

            if (!picked.Tab.Active)
                picked.Tab.Activate();

            return new BridgeExecutionContext(picked.InstanceId, picked.Tab, scope);
        }

        private BwTabMatch TryPickWndMatch(SelectorScope scope)
        {
            var matches = new List<BwTabMatch>();
            foreach (var client in _listClients())
            {
                var browser = GetClient(client.InstanceId).GetBrowser();
                foreach (var tab in browser.GetAllTabs())
                {
                    if (BridgeTabResolver.TabMatchesWnd(tab, scope.TabLevel))
                        matches.Add(new BwTabMatch(client.InstanceId, tab));
                }
            }

            return BridgeTabResolver.SelectMatch(matches, scope.TabLevel);
        }

        private static bool IsNoTabMatchedError(Exception ex)
        {
            return ex != null &&
                   ex.Message != null &&
                   ex.Message.IndexOf("No tab matched the <wnd> selector", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static DateTime CreateDeadline(int timeoutMs)
        {
            return DateTime.UtcNow.AddMilliseconds(Math.Max(0, timeoutMs));
        }

        private static int RemainingMs(DateTime deadline)
        {
            return Math.Max(0, (int)(deadline - DateTime.UtcNow).TotalMilliseconds);
        }
    }
}
