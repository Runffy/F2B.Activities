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

        public void Start()
        {
            if (_running)
            {
                return;
            }

            EnsureConnected();
            _running = true;
            _recvThread = new Thread(RecvLoop)
            {
                IsBackground = true,
                Name = "CdpClientRecv"
            };
            _recvThread.Start();
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

            if (_alertFlag &&
                (method.StartsWith("Runtime.", StringComparison.Ordinal) ||
                 method.StartsWith("Input.", StringComparison.Ordinal)))
            {
                throw new BrowserException("JavaScript dialog is open.");
            }

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
                    SendText(payload);
                }

                Dictionary<string, object> response;
                if (!queue.TryTake(out response, timeout))
                {
                    throw new BrowserException(string.Format("CDP command timed out: {0}", method));
                }

                object errorValue;
                if (response.TryGetValue("error", out errorValue) && errorValue != null)
                {
                    throw new BrowserException(string.Format("CDP command failed: {0}", errorValue));
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

        public void Enable(params string[] domains)
        {
            foreach (var domain in domains)
            {
                if (string.IsNullOrWhiteSpace(domain))
                {
                    continue;
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

            if (_socket != null)
            {
                try
                {
                    if (_socket.State == WebSocketState.Open)
                    {
                        _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "close", CancellationToken.None)
                            .GetAwaiter()
                            .GetResult();
                    }
                }
                catch
                {
                    // Ignore close errors.
                }

                _socket.Dispose();
                _socket = null;
            }

            if (_recvThread != null && _recvThread.IsAlive)
            {
                _recvThread.Join(TimeSpan.FromSeconds(2));
            }

            foreach (var pair in _pendingCommands)
            {
                BlockingCollection<Dictionary<string, object>> queue;
                if (_pendingCommands.TryRemove(pair.Key, out queue))
                {
                    queue.Dispose();
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

        private void EnsureConnected()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("CdpClient");
            }

            if (_socket != null && _socket.State == WebSocketState.Open)
            {
                return;
            }

            _socket = new ClientWebSocket();
            _socket.ConnectAsync(new Uri(_webSocketUrl), CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        }

        private void RecvLoop()
        {
            while (_running && !_disposed)
            {
                try
                {
                    if (_socket == null || _socket.State != WebSocketState.Open)
                    {
                        Thread.Sleep(10);
                        continue;
                    }

                    var responseText = ReceiveMessage(TimeSpan.FromSeconds(1));
                    if (string.IsNullOrEmpty(responseText))
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
                catch
                {
                    if (!_running || _disposed)
                    {
                        break;
                    }
                }
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

        private void SendText(string payload)
        {
            var bytes = Encoding.UTF8.GetBytes(payload);
            _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        }

        private string ReceiveMessage(TimeSpan timeout)
        {
            using (var cancellation = new CancellationTokenSource(timeout))
            {
                try
                {
                    return ReceiveMessageAsync(cancellation.Token).GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
            }
        }

        private async Task<string> ReceiveMessageAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[16384];
            var builder = new StringBuilder();

            while (_socket != null && _socket.State == WebSocketState.Open)
            {
                var segment = new ArraySegment<byte>(buffer);
                var result = await _socket.ReceiveAsync(segment, cancellationToken).ConfigureAwait(false);
                builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                if (result.EndOfMessage)
                {
                    break;
                }
            }

            return builder.ToString();
        }
    }
}
