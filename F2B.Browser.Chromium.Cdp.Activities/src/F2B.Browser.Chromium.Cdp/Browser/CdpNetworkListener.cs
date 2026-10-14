using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using F2B.Browser.Chromium.Cdp.Exceptions;
using F2B.Browser.Chromium.Cdp.Internal;

namespace F2B.Browser.Chromium.Cdp.Browser
{
    /// <summary>
    /// CDP Network listener aligned with DrissionPage <c>page.listen</c> / <c>frame.listen</c>.
    /// </summary>
    public sealed class CdpNetworkListener : IDisposable
    {
        private const int PollMs = 30;
        private const string RequestWillBeSent = "Network.requestWillBeSent";
        private const string RequestWillBeSentExtraInfo = "Network.requestWillBeSentExtraInfo";
        private const string ResponseReceived = "Network.responseReceived";
        private const string ResponseReceivedExtraInfo = "Network.responseReceivedExtraInfo";
        private const string LoadingFinished = "Network.loadingFinished";
        private const string LoadingFailed = "Network.loadingFailed";

        private readonly object _syncRoot = new object();
        private readonly CdpTab _tab;
        private readonly CdpFrame _frame;
        private BlockingCollection<NetworkEvent> _events = new BlockingCollection<NetworkEvent>();

        private CdpClient _client;
        private Thread _worker;
        private bool _disposed;
        private bool _listening;
        private bool _driverStarted;
        private string _attachedWebSocketUrl;
        private bool _filterByFrameId;
        private string _filterFrameId;

        private IList<string> _targets = new List<string>();
        private bool _isRegex;
        private CdpNetworkMethod _method = CdpNetworkMethod.Get | CdpNetworkMethod.Post;
        private CdpNetworkResourceType _resType = CdpNetworkResourceType.All;

        private readonly Dictionary<string, CdpNetworkPacket> _requestIds = new Dictionary<string, CdpNetworkPacket>();
        private readonly Dictionary<string, ExtraSlot> _extraInfoIds = new Dictionary<string, ExtraSlot>();
        private readonly Queue<CdpNetworkPacket> _caught = new Queue<CdpNetworkPacket>();
        private int _runningRequests;
        private int _runningTargets;

        internal CdpNetworkListener(CdpTab tab)
        {
            if (tab == null)
            {
                throw new ArgumentNullException("tab");
            }

            _tab = tab;
        }

        internal CdpNetworkListener(CdpFrame frame)
        {
            if (frame == null)
            {
                throw new ArgumentNullException("frame");
            }

            _frame = frame;
            _tab = frame.Tab;
        }

        public bool Listening
        {
            get { lock (_syncRoot) { return _listening; } }
        }

        public IList<string> Targets
        {
            get { lock (_syncRoot) { return _targets == null ? new List<string>() : new List<string>(_targets); } }
        }

        public static CdpNetworkListener From(CdpBase owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException("owner");
            }

            var tab = owner as CdpTab;
            if (tab != null)
            {
                return tab.Listen;
            }

            var frame = owner as CdpFrame;
            if (frame != null)
            {
                return frame.Listen;
            }

            throw new InvalidOperationException("Listen requires a CdpTab or CdpFrame.");
        }

        public void SetTargets(
            IList<string> targets = null,
            bool? isRegex = null,
            CdpNetworkMethod? method = null,
            CdpNetworkResourceType? resType = null)
        {
            lock (_syncRoot)
            {
                if (targets != null)
                {
                    _targets = new List<string>(targets);
                }

                if (isRegex.HasValue)
                {
                    _isRegex = isRegex.Value;
                }

                if (method.HasValue)
                {
                    _method = method.Value;
                }

                if (resType.HasValue)
                {
                    _resType = resType.Value;
                }
            }
        }

        public void Start(
            IList<string> targets = null,
            bool? isRegex = null,
            CdpNetworkMethod? method = null,
            CdpNetworkResourceType? resType = null)
        {
            if (targets != null && !isRegex.HasValue)
            {
                isRegex = false;
            }

            if (targets != null || isRegex.HasValue || method.HasValue || resType.HasValue)
            {
                SetTargets(targets, isRegex, method, resType);
            }

            Clear();

            lock (_syncRoot)
            {
                EnsureNotDisposed();
                if (_listening)
                {
                    EnsureAttachedUnlocked(forceReconnectIfChanged: true);
                    return;
                }

                EnsureAttachedUnlocked(forceReconnectIfChanged: false);
                BindMainCallbacksUnlocked();
                _listening = true;
            }
        }

        public CdpNetworkPacket[] Wait(int count = 1, int timeoutMs = 0, bool throwOnTimeout = true)
        {
            EnsureListening();
            if (count < 1)
            {
                count = 1;
            }

            var infinite = timeoutMs <= 0;
            var deadline = infinite ? DateTime.MaxValue : DateTime.UtcNow.AddMilliseconds(timeoutMs);
            var failed = false;

            while (IsDriverRunning() && IsListening())
            {
                lock (_syncRoot)
                {
                    if (_caught.Count >= count)
                    {
                        failed = false;
                        break;
                    }
                }

                if (!infinite && DateTime.UtcNow >= deadline)
                {
                    failed = true;
                    break;
                }

                Thread.Sleep(PollMs);
            }

            if (!IsListening() && !HasCaught(count))
            {
                failed = true;
            }

            if (failed)
            {
                if (throwOnTimeout)
                {
                    throw new BrowserException(
                        string.Format("Timed out after {0} ms waiting for {1} network packet(s).", timeoutMs, count));
                }

                return new CdpNetworkPacket[0];
            }

            lock (_syncRoot)
            {
                var result = new CdpNetworkPacket[count];
                for (var i = 0; i < count; i++)
                {
                    result[i] = _caught.Dequeue();
                }

                return result;
            }
        }

        public IEnumerable<CdpNetworkPacket[]> Steps(int? count = null, int timeoutMs = 0, int gap = 1)
        {
            EnsureListening();
            if (gap < 1)
            {
                gap = 1;
            }

            var caught = 0;
            while (IsDriverRunning() && IsListening())
            {
                var batch = Wait(gap, timeoutMs, throwOnTimeout: false);
                if (batch == null || batch.Length == 0)
                {
                    yield break;
                }

                yield return batch;
                caught += batch.Length;
                if (count.HasValue && caught >= count.Value)
                {
                    yield break;
                }
            }
        }

        public bool WaitSilent(int timeoutMs = 0, bool targetsOnly = false, int limit = 0, bool throwOnTimeout = true)
        {
            EnsureListening();
            var infinite = timeoutMs <= 0;
            var deadline = infinite ? DateTime.MaxValue : DateTime.UtcNow.AddMilliseconds(timeoutMs);

            while (infinite || DateTime.UtcNow < deadline)
            {
                lock (_syncRoot)
                {
                    var running = targetsOnly ? _runningTargets : _runningRequests;
                    if (running <= limit)
                    {
                        return true;
                    }
                }

                Thread.Sleep(10);
            }

            if (throwOnTimeout)
            {
                throw new BrowserException(
                    string.Format("Timed out after {0} ms waiting for network to become silent.", timeoutMs));
            }

            return false;
        }

        public void Pause(bool clear = true)
        {
            lock (_syncRoot)
            {
                if (_listening && _client != null)
                {
                    UnbindMainCallbacksUnlocked();
                    _listening = false;
                }
            }

            if (clear)
            {
                Clear();
            }
        }

        public void Resume()
        {
            lock (_syncRoot)
            {
                EnsureNotDisposed();
                if (_listening)
                {
                    return;
                }

                if (!_driverStarted || _client == null)
                {
                    throw new BrowserException("Cannot resume listen: start listening first (Resume is only valid after Pause).");
                }

                BindMainCallbacksUnlocked();
                _listening = true;
            }
        }

        public void Stop()
        {
            Pause(clear: true);
            DisposeDriver();
        }

        public void Clear()
        {
            lock (_syncRoot)
            {
                _requestIds.Clear();
                _extraInfoIds.Clear();
                _caught.Clear();
                _runningRequests = 0;
                _runningTargets = 0;
            }
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                _disposed = true;
            }

            Stop();
        }

        private void EnsureListening()
        {
            if (!IsListening())
            {
                throw new BrowserException("Network listen is not started.");
            }
        }

        private bool IsListening()
        {
            lock (_syncRoot)
            {
                return _listening;
            }
        }

        private bool IsDriverRunning()
        {
            lock (_syncRoot)
            {
                return _client != null && _client.IsConnected && !_disposed;
            }
        }

        private bool HasCaught(int count)
        {
            lock (_syncRoot)
            {
                return _caught.Count >= count;
            }
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("CdpNetworkListener");
            }
        }

        private void EnsureAttachedUnlocked(bool forceReconnectIfChanged)
        {
            var attach = ResolveAttach();
            if (_driverStarted && _client != null &&
                string.Equals(_attachedWebSocketUrl, attach.WebSocketUrl, StringComparison.Ordinal) &&
                _filterByFrameId == attach.FilterByFrameId &&
                string.Equals(_filterFrameId, attach.FrameId, StringComparison.Ordinal))
            {
                return;
            }

            if (_driverStarted && !forceReconnectIfChanged && _client != null)
            {
                return;
            }

            StartDriverUnlocked(attach);
        }

        private ListenAttach ResolveAttach()
        {
            if (_frame == null)
            {
                return new ListenAttach
                {
                    WebSocketUrl = _tab.WebSocketDebuggerUrl,
                    FilterByFrameId = false,
                    FrameId = null
                };
            }

            var diffDomain = !_frame.IsInnerFrame();
            if (diffDomain)
            {
                return new ListenAttach
                {
                    WebSocketUrl = CdpJsonClient.ResolveTargetWebSocketUrl(_tab.Browser.Port, _frame.FrameId),
                    FilterByFrameId = false,
                    FrameId = _frame.FrameId
                };
            }

            return new ListenAttach
            {
                WebSocketUrl = _tab.WebSocketDebuggerUrl,
                FilterByFrameId = true,
                FrameId = _frame.FrameId
            };
        }

        private void StartDriverUnlocked(ListenAttach attach)
        {
            DisposeDriverUnlocked();
            _events = new BlockingCollection<NetworkEvent>();

            if (string.IsNullOrWhiteSpace(attach.WebSocketUrl))
            {
                throw new BrowserException("Tab has no WebSocketDebuggerUrl for network listen.");
            }

            _client = new CdpClient(attach.WebSocketUrl);
            _client.Start();
            BindAllCallbacksUnlocked();
            _client.Enable("Network");

            _attachedWebSocketUrl = attach.WebSocketUrl;
            _filterByFrameId = attach.FilterByFrameId;
            _filterFrameId = attach.FrameId;
            _driverStarted = true;

            _worker = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "CdpNetworkListen"
            };
            _worker.Start();
        }

        private void BindAllCallbacksUnlocked()
        {
            if (_client == null)
            {
                return;
            }

            _client.SetCallback(RequestWillBeSent, p => Enqueue(RequestWillBeSent, p), immediate: true);
            _client.SetCallback(RequestWillBeSentExtraInfo, p => Enqueue(RequestWillBeSentExtraInfo, p), immediate: true);
            _client.SetCallback(ResponseReceived, p => Enqueue(ResponseReceived, p), immediate: true);
            _client.SetCallback(ResponseReceivedExtraInfo, p => Enqueue(ResponseReceivedExtraInfo, p), immediate: true);
            _client.SetCallback(LoadingFinished, p => Enqueue(LoadingFinished, p), immediate: true);
            _client.SetCallback(LoadingFailed, p => Enqueue(LoadingFailed, p), immediate: true);
        }

        private void BindMainCallbacksUnlocked()
        {
            if (_client == null)
            {
                return;
            }

            _client.SetCallback(RequestWillBeSent, p => Enqueue(RequestWillBeSent, p), immediate: true);
            _client.SetCallback(ResponseReceived, p => Enqueue(ResponseReceived, p), immediate: true);
            _client.SetCallback(LoadingFinished, p => Enqueue(LoadingFinished, p), immediate: true);
            _client.SetCallback(LoadingFailed, p => Enqueue(LoadingFailed, p), immediate: true);
        }

        private void UnbindMainCallbacksUnlocked()
        {
            if (_client == null)
            {
                return;
            }

            _client.SetCallback(RequestWillBeSent, null, immediate: true);
            _client.SetCallback(ResponseReceived, null, immediate: true);
            _client.SetCallback(LoadingFinished, null, immediate: true);
            _client.SetCallback(LoadingFailed, null, immediate: true);
        }

        private void Enqueue(string name, Dictionary<string, object> parameters)
        {
            try
            {
                _events.Add(new NetworkEvent { Name = name, Parameters = parameters ?? new Dictionary<string, object>() });
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void WorkerLoop()
        {
            try
            {
                foreach (var item in _events.GetConsumingEnumerable())
                {
                    try
                    {
                        Dispatch(item);
                    }
                    catch
                    {
                    }
                }
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void Dispatch(NetworkEvent item)
        {
            switch (item.Name)
            {
                case RequestWillBeSent:
                    OnRequestWillBeSent(item.Parameters);
                    break;
                case RequestWillBeSentExtraInfo:
                    OnRequestWillBeSentExtraInfo(item.Parameters);
                    break;
                case ResponseReceived:
                    OnResponseReceived(item.Parameters);
                    break;
                case ResponseReceivedExtraInfo:
                    OnResponseReceivedExtraInfo(item.Parameters);
                    break;
                case LoadingFinished:
                    OnLoadingFinished(item.Parameters);
                    break;
                case LoadingFailed:
                    OnLoadingFailed(item.Parameters);
                    break;
            }
        }

        private bool ShouldFilterFrame(Dictionary<string, object> parameters)
        {
            lock (_syncRoot)
            {
                if (!_filterByFrameId)
                {
                    return false;
                }

                var frameId = CdpValueConverter.GetString(parameters, "frameId");
                return !string.Equals(frameId, _filterFrameId, StringComparison.Ordinal);
            }
        }

        private void OnRequestWillBeSent(Dictionary<string, object> kwargs)
        {
            if (ShouldFilterFrame(kwargs))
            {
                return;
            }

            CdpNetworkPacket packet = null;
            string rid;
            IList<string> targets;
            bool isRegex;
            CdpNetworkMethod methodFilter;
            CdpNetworkResourceType resFilter;
            string tabId;

            lock (_syncRoot)
            {
                _runningRequests++;
                targets = _targets;
                isRegex = _isRegex;
                methodFilter = _method;
                resFilter = _resType;
                tabId = _tab.TabId;
            }

            var request = CdpValueConverter.GetDictionary(kwargs, "request") ?? new Dictionary<string, object>();
            var url = CdpValueConverter.GetString(request, "url") ?? string.Empty;
            var httpMethod = CdpValueConverter.GetString(request, "method") ?? string.Empty;
            var resType = CdpValueConverter.GetString(kwargs, "type") ?? string.Empty;
            rid = CdpValueConverter.GetString(kwargs, "requestId");

            var methodOk = MethodMatches(methodFilter, httpMethod);
            var resOk = ResourceMatches(resFilter, resType);
            var matchTarget = MatchTarget(targets, isRegex, url);

            if (methodOk && resOk && matchTarget.Matched)
            {
                lock (_syncRoot)
                {
                    _runningTargets++;
                    packet = new CdpNetworkPacket(tabId, matchTarget.Target);
                    packet.RawRequest = kwargs;
                    packet.FrameId = CdpValueConverter.GetString(kwargs, "frameId");
                    _requestIds[rid ?? string.Empty] = packet;
                }

                if (CdpValueConverter.GetBool(request, "hasPostData") &&
                    string.IsNullOrEmpty(CdpValueConverter.GetString(request, "postData")))
                {
                    packet.RawPostData = TryGetRequestPostData(rid);
                }
            }

            lock (_syncRoot)
            {
                ExtraSlot slot;
                if (!_extraInfoIds.TryGetValue(rid ?? string.Empty, out slot))
                {
                    slot = new ExtraSlot();
                    _extraInfoIds[rid ?? string.Empty] = slot;
                }

                slot.Packet = packet;
                slot.Rejected = packet == null;
            }
        }

        private void OnRequestWillBeSentExtraInfo(Dictionary<string, object> kwargs)
        {
            var rid = CdpValueConverter.GetString(kwargs, "requestId") ?? string.Empty;
            lock (_syncRoot)
            {
                _runningRequests++;
                ExtraSlot slot;
                if (!_extraInfoIds.TryGetValue(rid, out slot))
                {
                    slot = new ExtraSlot();
                    _extraInfoIds[rid] = slot;
                }

                slot.RequestExtra = kwargs;
            }
        }

        private void OnResponseReceived(Dictionary<string, object> kwargs)
        {
            if (ShouldFilterFrame(kwargs))
            {
                return;
            }

            var rid = CdpValueConverter.GetString(kwargs, "requestId") ?? string.Empty;
            lock (_syncRoot)
            {
                CdpNetworkPacket packet;
                if (_requestIds.TryGetValue(rid, out packet) && packet != null)
                {
                    packet.RawResponse = CdpValueConverter.GetDictionary(kwargs, "response");
                    packet.ResourceType = CdpValueConverter.GetString(kwargs, "type");
                }
            }
        }

        private void OnResponseReceivedExtraInfo(Dictionary<string, object> kwargs)
        {
            var rid = CdpValueConverter.GetString(kwargs, "requestId") ?? string.Empty;
            lock (_syncRoot)
            {
                _runningRequests--;
                ExtraSlot slot;
                if (!_extraInfoIds.TryGetValue(rid, out slot))
                {
                    return;
                }

                if (slot.Rejected)
                {
                    _extraInfoIds.Remove(rid);
                    return;
                }

                if (slot.Packet != null)
                {
                    slot.Packet.SetRequestExtraInfo(slot.RequestExtra);
                    slot.Packet.SetResponseExtraInfo(kwargs);
                    _extraInfoIds.Remove(rid);
                    return;
                }

                slot.ResponseExtra = kwargs;
            }
        }

        private void OnLoadingFinished(Dictionary<string, object> kwargs)
        {
            var rid = CdpValueConverter.GetString(kwargs, "requestId") ?? string.Empty;
            CdpNetworkPacket packet;
            lock (_syncRoot)
            {
                _runningRequests--;
                _requestIds.TryGetValue(rid, out packet);
            }

            if (packet != null)
            {
                ApplyResponseBody(packet, rid);
                var request = CdpValueConverter.GetDictionary(packet.RawRequest, "request");
                if (request != null &&
                    CdpValueConverter.GetBool(request, "hasPostData") &&
                    string.IsNullOrEmpty(CdpValueConverter.GetString(request, "postData")) &&
                    string.IsNullOrEmpty(packet.RawPostData))
                {
                    packet.RawPostData = TryGetRequestPostData(rid, TimeSpan.FromSeconds(1));
                }
            }

            lock (_syncRoot)
            {
                ExtraSlot slot;
                if (_extraInfoIds.TryGetValue(rid, out slot))
                {
                    if (slot.Rejected || (slot.Packet != null && slot.RequestExtra == null))
                    {
                        _extraInfoIds.Remove(rid);
                    }
                    else if (slot.Packet != null && slot.ResponseExtra != null)
                    {
                        slot.Packet.SetRequestExtraInfo(slot.RequestExtra);
                        slot.Packet.SetResponseExtraInfo(slot.ResponseExtra);
                        _extraInfoIds.Remove(rid);
                    }
                }

                _requestIds.Remove(rid);
                if (packet != null)
                {
                    _caught.Enqueue(packet);
                    _runningTargets--;
                }
            }
        }

        private void OnLoadingFailed(Dictionary<string, object> kwargs)
        {
            var rid = CdpValueConverter.GetString(kwargs, "requestId") ?? string.Empty;
            CdpNetworkPacket packet;
            lock (_syncRoot)
            {
                _runningRequests--;
                if (_requestIds.TryGetValue(rid, out packet) && packet != null)
                {
                    packet.RawFailInfo = kwargs;
                    packet.ResourceType = CdpValueConverter.GetString(kwargs, "type");
                    packet.IsFailed = true;
                }

                ExtraSlot slot;
                if (_extraInfoIds.TryGetValue(rid, out slot))
                {
                    if (slot.Rejected && slot.ResponseExtra != null)
                    {
                        _extraInfoIds.Remove(rid);
                    }
                    else if (slot.Packet != null && slot.ResponseExtra != null)
                    {
                        slot.Packet.SetRequestExtraInfo(slot.RequestExtra);
                        slot.Packet.SetResponseExtraInfo(slot.ResponseExtra);
                        _extraInfoIds.Remove(rid);
                    }
                }

                _requestIds.Remove(rid);
                if (packet != null)
                {
                    _caught.Enqueue(packet);
                    _runningTargets--;
                }
            }
        }

        private void ApplyResponseBody(CdpNetworkPacket packet, string requestId)
        {
            CdpClient client;
            lock (_syncRoot)
            {
                client = _client;
            }

            if (client == null)
            {
                packet.RawBody = string.Empty;
                packet.Base64Body = false;
                return;
            }

            try
            {
                var result = client.Send("Network.getResponseBody", new Dictionary<string, object>
                {
                    { "requestId", requestId }
                });
                if (result != null && result.ContainsKey("body"))
                {
                    packet.RawBody = CdpValueConverter.GetString(result, "body") ?? string.Empty;
                    packet.Base64Body = CdpValueConverter.GetBool(result, "base64Encoded");
                }
                else
                {
                    packet.RawBody = string.Empty;
                    packet.Base64Body = false;
                }
            }
            catch
            {
                packet.RawBody = string.Empty;
                packet.Base64Body = false;
            }
        }

        private string TryGetRequestPostData(string requestId, TimeSpan? timeout = null)
        {
            CdpClient client;
            lock (_syncRoot)
            {
                client = _client;
            }

            if (client == null || string.IsNullOrEmpty(requestId))
            {
                return null;
            }

            try
            {
                var result = timeout.HasValue
                    ? client.Send("Network.getRequestPostData", new Dictionary<string, object> { { "requestId", requestId } }, timeout)
                    : client.Send("Network.getRequestPostData", new Dictionary<string, object> { { "requestId", requestId } });
                return CdpValueConverter.GetString(result, "postData");
            }
            catch
            {
                return null;
            }
        }

        private void DisposeDriver()
        {
            lock (_syncRoot)
            {
                DisposeDriverUnlocked();
            }
        }

        private void DisposeDriverUnlocked()
        {
            _listening = false;
            _driverStarted = false;
            _attachedWebSocketUrl = null;

            if (_client != null)
            {
                try
                {
                    _client.Dispose();
                }
                catch
                {
                }

                _client = null;
            }

            try
            {
                _events.CompleteAdding();
            }
            catch
            {
            }

            var worker = _worker;
            _worker = null;
            if (worker != null && worker.IsAlive && Thread.CurrentThread != worker)
            {
                worker.Join(TimeSpan.FromSeconds(2));
            }
        }

        private static TargetMatch MatchTarget(IList<string> targets, bool isRegex, string url)
        {
            if (targets == null || targets.Count == 0)
            {
                return new TargetMatch { Matched = true, Target = null };
            }

            foreach (var target in targets)
            {
                if (string.IsNullOrEmpty(target))
                {
                    continue;
                }

                var hit = isRegex
                    ? SafeRegex(target, url)
                    : url.IndexOf(target, StringComparison.Ordinal) >= 0;
                if (hit)
                {
                    return new TargetMatch { Matched = true, Target = target };
                }
            }

            return new TargetMatch { Matched = false, Target = null };
        }

        private static bool SafeRegex(string pattern, string url)
        {
            try
            {
                return Regex.IsMatch(url ?? string.Empty, pattern);
            }
            catch
            {
                return false;
            }
        }

        private static bool MethodMatches(CdpNetworkMethod filter, string method)
        {
            if ((filter & CdpNetworkMethod.All) == CdpNetworkMethod.All)
            {
                return true;
            }

            CdpNetworkMethod flag;
            if (!TryParseMethod(method, out flag))
            {
                return false;
            }

            return (filter & flag) == flag;
        }

        private static bool TryParseMethod(string method, out CdpNetworkMethod flag)
        {
            flag = CdpNetworkMethod.None;
            if (string.IsNullOrEmpty(method))
            {
                return false;
            }

            switch (method.Trim().ToUpperInvariant())
            {
                case "GET":
                    flag = CdpNetworkMethod.Get;
                    return true;
                case "POST":
                    flag = CdpNetworkMethod.Post;
                    return true;
                case "PUT":
                    flag = CdpNetworkMethod.Put;
                    return true;
                case "DELETE":
                    flag = CdpNetworkMethod.Delete;
                    return true;
                case "PATCH":
                    flag = CdpNetworkMethod.Patch;
                    return true;
                case "HEAD":
                    flag = CdpNetworkMethod.Head;
                    return true;
                case "OPTIONS":
                    flag = CdpNetworkMethod.Options;
                    return true;
                default:
                    return false;
            }
        }

        private static bool ResourceMatches(CdpNetworkResourceType filter, string resType)
        {
            if ((filter & CdpNetworkResourceType.All) == CdpNetworkResourceType.All)
            {
                return true;
            }

            CdpNetworkResourceType flag;
            if (!TryParseResource(resType, out flag))
            {
                return false;
            }

            return (filter & flag) == flag;
        }

        private static bool TryParseResource(string resType, out CdpNetworkResourceType flag)
        {
            flag = CdpNetworkResourceType.None;
            if (string.IsNullOrEmpty(resType))
            {
                return false;
            }

            switch (resType.Trim().ToUpperInvariant())
            {
                case "DOCUMENT":
                    flag = CdpNetworkResourceType.Document;
                    return true;
                case "STYLESHEET":
                    flag = CdpNetworkResourceType.Stylesheet;
                    return true;
                case "IMAGE":
                    flag = CdpNetworkResourceType.Image;
                    return true;
                case "MEDIA":
                    flag = CdpNetworkResourceType.Media;
                    return true;
                case "FONT":
                    flag = CdpNetworkResourceType.Font;
                    return true;
                case "SCRIPT":
                    flag = CdpNetworkResourceType.Script;
                    return true;
                case "TEXTTRACK":
                    flag = CdpNetworkResourceType.TextTrack;
                    return true;
                case "XHR":
                    flag = CdpNetworkResourceType.Xhr;
                    return true;
                case "FETCH":
                    flag = CdpNetworkResourceType.Fetch;
                    return true;
                case "PREFETCH":
                    flag = CdpNetworkResourceType.Prefetch;
                    return true;
                case "EVENTSOURCE":
                    flag = CdpNetworkResourceType.EventSource;
                    return true;
                case "WEBSOCKET":
                    flag = CdpNetworkResourceType.WebSocket;
                    return true;
                case "MANIFEST":
                    flag = CdpNetworkResourceType.Manifest;
                    return true;
                case "SIGNEDEXCHANGE":
                    flag = CdpNetworkResourceType.SignedExchange;
                    return true;
                case "PING":
                    flag = CdpNetworkResourceType.Ping;
                    return true;
                case "CSPVIOLATIONREPORT":
                    flag = CdpNetworkResourceType.CspViolationReport;
                    return true;
                case "PREFLIGHT":
                    flag = CdpNetworkResourceType.Preflight;
                    return true;
                case "OTHER":
                    flag = CdpNetworkResourceType.Other;
                    return true;
                default:
                    return false;
            }
        }

        private sealed class ExtraSlot
        {
            public CdpNetworkPacket Packet;
            public bool Rejected;
            public Dictionary<string, object> RequestExtra;
            public Dictionary<string, object> ResponseExtra;
        }

        private sealed class NetworkEvent
        {
            public string Name;
            public Dictionary<string, object> Parameters;
        }

        private sealed class ListenAttach
        {
            public string WebSocketUrl;
            public bool FilterByFrameId;
            public string FrameId;
        }

        private struct TargetMatch
        {
            public bool Matched;
            public string Target;
        }
    }
}
