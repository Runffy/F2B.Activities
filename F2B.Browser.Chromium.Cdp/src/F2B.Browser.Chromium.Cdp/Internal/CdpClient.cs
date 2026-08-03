using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using F2B.Browser.Chromium.Cdp.Exceptions;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal sealed class CdpClient : IDisposable
    {
        private readonly string _webSocketUrl;
        private readonly TimeSpan _commandTimeout;
        private readonly CdpJsonSerializer _serializer = new CdpJsonSerializer();
        private readonly object _connectLock = new object();
        private readonly object _sendLock = new object();
        private readonly ConcurrentDictionary<int, BlockingCollection<Dictionary<string, object>>> _pendingCommands =
            new ConcurrentDictionary<int, BlockingCollection<Dictionary<string, object>>>();
        private readonly Dictionary<string, Action<Dictionary<string, object>>> _eventHandlers =
            new Dictionary<string, Action<Dictionary<string, object>>>(StringComparer.Ordinal);
        private readonly Dictionary<string, Action<Dictionary<string, object>>> _immediateEventHandlers =
            new Dictionary<string, Action<Dictionary<string, object>>>(StringComparer.Ordinal);

        private ClientWebSocket _socket;
        private Thread _recvThread;
        private int _messageId;
        private volatile bool _disposed;
        private volatile bool _running;
        private volatile bool _alertFlag;
        private volatile int _connectionGeneration;
        private readonly List<string> _enabledDomains = new List<string>();

        /// <summary>
        /// Raised on the receive thread after a successful reconnect (domains not yet re-enabled).
        /// Handlers should be quick and thread-safe.
        /// </summary>
        public event Action ConnectionRestored;

        public CdpClient(string webSocketUrl, TimeSpan? commandTimeout = null)
        {
            if (string.IsNullOrWhiteSpace(webSocketUrl))
            {
                throw new ArgumentNullException("webSocketUrl");
            }

            _webSocketUrl = webSocketUrl;
            _commandTimeout = commandTimeout ?? TimeSpan.FromSeconds(30);
        }

        public bool AlertFlag
        {
            get { return _alertFlag; }
        }

        public bool IsConnected
        {
            get
            {
                var socket = _socket;
                return socket != null && socket.State == WebSocketState.Open;
            }
        }

        public void Start()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("CdpClient");
            }

            lock (_connectLock)
            {
                if (_running)
                {
                    return;
                }

                ConnectSocketUnlocked();
                _running = true;
                _recvThread = new Thread(RecvLoop)
                {
                    IsBackground = true,
                    Name = "CdpClientRecv"
                };
                _recvThread.Start();
            }
        }

        public void SetCallback(string eventName, Action<Dictionary<string, object>> handler, bool immediate = false)
        {
            var handlers = immediate ? _immediateEventHandlers : _eventHandlers;
            if (handler == null)
            {
                handlers.Remove(eventName);
            }
            else
            {
                handlers[eventName] = handler;
            }
        }

        public Dictionary<string, object> Send(string method, Dictionary<string, object> parameters = null)
        {
            return Send(method, parameters, null);
        }

        public Dictionary<string, object> Send(
            string method,
            Dictionary<string, object> parameters,
            TimeSpan? commandTimeout)
        {
            EnsureStarted();
            EnsureConnectedForSend();

            if (_alertFlag &&
                (method.StartsWith("Runtime.", StringComparison.Ordinal) ||
                 method.StartsWith("Input.", StringComparison.Ordinal)))
            {
                throw new BrowserException("JavaScript dialog is open.");
            }

            try
            {
                return SendOnce(method, parameters, commandTimeout);
            }
            catch (Exception ex)
            {
                if (!IsTransientTransportFailure(ex))
                {
                    throw;
                }

                // One automatic reconnect + retry for aborted / closed sockets.
                EnsureConnectedForSend(forceReconnect: true);
                return SendOnce(method, parameters, commandTimeout);
            }
        }

        public void Enable(params string[] domains)
        {
            foreach (var domain in domains)
            {
                if (string.IsNullOrWhiteSpace(domain))
                {
                    continue;
                }

                lock (_enabledDomains)
                {
                    if (!_enabledDomains.Contains(domain))
                    {
                        _enabledDomains.Add(domain);
                    }
                }

                Send(domain + ".enable");
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _running = false;

            FailPendingCommands("CDP client disposed.");
            DisposeSocket();

            if (_recvThread != null && _recvThread.IsAlive)
            {
                _recvThread.Join(TimeSpan.FromSeconds(2));
            }
        }

        private Dictionary<string, object> SendOnce(
            string method,
            Dictionary<string, object> parameters,
            TimeSpan? commandTimeout)
        {
            var timeout = commandTimeout ?? _commandTimeout;
            var id = Interlocked.Increment(ref _messageId);
            var queue = new BlockingCollection<Dictionary<string, object>>();
            if (!_pendingCommands.TryAdd(id, queue))
            {
                throw new BrowserException("Failed to register CDP command.");
            }

            try
            {
                var payload = parameters == null || parameters.Count == 0
                    ? _serializer.Serialize(new { id, method })
                    : _serializer.Serialize(new { id, method, @params = parameters });

                lock (_sendLock)
                {
                    SendTextUnlocked(payload);
                }

                Dictionary<string, object> response;
                if (!queue.TryTake(out response, timeout))
                {
                    throw new BrowserException(string.Format("CDP command timed out: {0}", method));
                }

                object errorValue;
                if (response.TryGetValue("error", out errorValue) && errorValue != null)
                {
                    throw new BrowserException(
                        string.Format("CDP command failed ({0}): {1}", method, CdpErrorFormatter.Format(errorValue)));
                }

                object resultValue;
                if (response.TryGetValue("result", out resultValue) && resultValue is Dictionary<string, object>)
                {
                    return (Dictionary<string, object>)resultValue;
                }

                return new Dictionary<string, object>();
            }
            finally
            {
                BlockingCollection<Dictionary<string, object>> removed;
                _pendingCommands.TryRemove(id, out removed);
                if (removed != null)
                {
                    removed.Dispose();
                }
            }
        }

        private void EnsureStarted()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("CdpClient");
            }

            if (!_running)
            {
                Start();
            }
        }

        private void EnsureConnectedForSend(bool forceReconnect = false)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("CdpClient");
            }

            var restored = false;
            lock (_connectLock)
            {
                if (!forceReconnect && IsConnected)
                {
                    return;
                }

                ConnectSocketUnlocked();
                restored = true;
            }

            if (restored)
            {
                // Must not wait for CDP responses while holding _connectLock (RecvLoop may need it).
                ReEnableDomains();
                RaiseConnectionRestored();
            }
        }

        private void ConnectSocketUnlocked()
        {
            DisposeSocketUnlocked();

            var socket = new ClientWebSocket();
            try
            {
                socket.ConnectAsync(new Uri(_webSocketUrl), CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception ex)
            {
                try
                {
                    socket.Dispose();
                }
                catch
                {
                }

                throw new BrowserException(
                    string.Format("Failed to connect CDP WebSocket: {0}", ex.Message),
                    ex);
            }

            _socket = socket;
            Interlocked.Increment(ref _connectionGeneration);
            _alertFlag = false;
        }

        private void ReEnableDomains()
        {
            string[] domains;
            lock (_enabledDomains)
            {
                domains = _enabledDomains.ToArray();
            }

            foreach (var domain in domains)
            {
                try
                {
                    SendOnce(domain + ".enable", null, null);
                }
                catch
                {
                    // Domain enable will be retried by the next public Send/Enable.
                }
            }
        }

        private void RaiseConnectionRestored()
        {
            var handler = ConnectionRestored;
            if (handler != null)
            {
                try
                {
                    handler();
                }
                catch
                {
                    // Session restore errors should not break transport.
                }
            }
        }

        private void RecvLoop()
        {
            while (_running && !_disposed)
            {
                var generation = _connectionGeneration;
                try
                {
                    if (!IsConnected)
                    {
                        Thread.Sleep(50);
                        continue;
                    }

                    // IMPORTANT: never cancel ReceiveAsync with a timeout token.
                    // Canceling ClientWebSocket.ReceiveAsync transitions the socket to Aborted
                    // and permanently kills the connection (.NET documented behavior).
                    var responseText = ReceiveMessageBlocking();
                    if (responseText == null)
                    {
                        HandleTransportLoss(generation);
                        continue;
                    }

                    if (responseText.Length == 0)
                    {
                        continue;
                    }

                    var dict = _serializer.DeserializeObject(responseText) as Dictionary<string, object>;
                    if (dict == null)
                    {
                        continue;
                    }

                    object methodValue;
                    if (dict.TryGetValue("method", out methodValue) && methodValue != null)
                    {
                        var method = Convert.ToString(methodValue);
                        if (method.StartsWith("Page.javascriptDialog", StringComparison.Ordinal))
                        {
                            _alertFlag = method.EndsWith("Opening", StringComparison.Ordinal);
                        }

                        Dictionary<string, object> parameters = null;
                        object paramsValue;
                        if (dict.TryGetValue("params", out paramsValue))
                        {
                            parameters = paramsValue as Dictionary<string, object>;
                        }

                        DispatchEvent(method, parameters ?? new Dictionary<string, object>());
                        continue;
                    }

                    object responseId;
                    if (!dict.TryGetValue("id", out responseId) || responseId == null)
                    {
                        continue;
                    }

                    var id = Convert.ToInt32(responseId);
                    BlockingCollection<Dictionary<string, object>> queue;
                    if (_pendingCommands.TryGetValue(id, out queue))
                    {
                        queue.TryAdd(dict);
                    }
                }
                catch (Exception)
                {
                    if (!_running || _disposed)
                    {
                        break;
                    }

                    HandleTransportLoss(generation);
                }
            }
        }

        private void HandleTransportLoss(int generation)
        {
            FailPendingCommands("CDP WebSocket disconnected.");

            lock (_connectLock)
            {
                // Another thread may already have reconnected.
                if (generation != _connectionGeneration && IsConnected)
                {
                    return;
                }

                DisposeSocketUnlocked();
            }
        }

        private void DispatchEvent(string method, Dictionary<string, object> parameters)
        {
            Action<Dictionary<string, object>> immediateHandler;
            if (_immediateEventHandlers.TryGetValue(method, out immediateHandler))
            {
                immediateHandler(parameters);
                return;
            }

            Action<Dictionary<string, object>> handler;
            if (_eventHandlers.TryGetValue(method, out handler))
            {
                handler(parameters);
            }
        }

        private void SendTextUnlocked(string payload)
        {
            var socket = _socket;
            if (socket == null || socket.State != WebSocketState.Open)
            {
                throw new WebSocketException(
                    "The WebSocket is in an invalid state ('" +
                    (socket == null ? "None" : socket.State.ToString()) +
                    "') for this operation. Valid states are 'Open, CloseReceived'");
            }

            var bytes = Encoding.UTF8.GetBytes(payload);
            socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        }

        private string ReceiveMessageBlocking()
        {
            return ReceiveMessageAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        private async Task<string> ReceiveMessageAsync(CancellationToken cancellationToken)
        {
            var socket = _socket;
            if (socket == null || socket.State != WebSocketState.Open)
            {
                return null;
            }

            var buffer = new byte[16384];
            var builder = new StringBuilder();

            while (socket.State == WebSocketState.Open)
            {
                var segment = new ArraySegment<byte>(buffer);
                WebSocketReceiveResult result;
                try
                {
                    result = await socket.ReceiveAsync(segment, cancellationToken).ConfigureAwait(false);
                }
                catch (WebSocketException)
                {
                    return null;
                }
                catch (ObjectDisposedException)
                {
                    return null;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    try
                    {
                        if (socket.State == WebSocketState.CloseReceived)
                        {
                            await socket.CloseAsync(
                                    WebSocketCloseStatus.NormalClosure,
                                    "ack close",
                                    CancellationToken.None)
                                .ConfigureAwait(false);
                        }
                    }
                    catch
                    {
                    }

                    return null;
                }

                builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                if (result.EndOfMessage)
                {
                    break;
                }
            }

            return builder.ToString();
        }

        private void FailPendingCommands(string reason)
        {
            var error = new Dictionary<string, object>
            {
                {
                    "error",
                    new Dictionary<string, object>
                    {
                        { "code", -32001 },
                        { "message", reason }
                    }
                }
            };

            foreach (var pair in _pendingCommands)
            {
                try
                {
                    pair.Value.TryAdd(error);
                }
                catch
                {
                }
            }
        }

        private void DisposeSocket()
        {
            lock (_connectLock)
            {
                DisposeSocketUnlocked();
            }
        }

        private void DisposeSocketUnlocked()
        {
            var socket = _socket;
            _socket = null;
            if (socket == null)
            {
                return;
            }

            try
            {
                if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
                {
                    socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "close", CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();
                }
            }
            catch
            {
                // Ignore close errors (including Aborted sockets).
            }

            try
            {
                socket.Dispose();
            }
            catch
            {
            }
        }

        private static bool IsTransientTransportFailure(Exception ex)
        {
            for (var current = ex; current != null; current = current.InnerException)
            {
                var ws = current as WebSocketException;
                if (ws != null)
                {
                    return true;
                }

                var message = current.Message ?? string.Empty;
                if (message.IndexOf("invalid state", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    message.IndexOf("Aborted", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    message.IndexOf("WebSocket", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
