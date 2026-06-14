using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace F2B.Browser.Chromium.Bridge
{
    public sealed class BridgeWebSocketServer : IDisposable
    {
        private sealed class ClientSession
        {
            public ClientSession(WebSocket socket)
            {
                Socket = socket;
                ConnectedAtUtc = DateTime.UtcNow;
                LastActivityUtc = DateTime.UtcNow;
            }

            public WebSocket Socket { get; }
            public string InstanceId { get; set; }
            public string Label { get; set; }
            public BridgeClientRole Role { get; set; } = BridgeClientRole.Extension;
            public DateTime ConnectedAtUtc { get; }
            public DateTime LastActivityUtc { get; set; }
            public bool IsRegistered { get; set; }
        }

        private static readonly JavaScriptSerializer JsonSerializer = new JavaScriptSerializer
        {
            MaxJsonLength = int.MaxValue,
            RecursionLimit = 64
        };

        private readonly object _sync = new object();
        private readonly Dictionary<string, ClientSession> _clientsByInstanceId = new Dictionary<string, ClientSession>();
        private readonly Dictionary<string, ClientSession> _controllersById = new Dictionary<string, ClientSession>();
        private readonly Dictionary<WebSocket, ClientSession> _sessionsBySocket = new Dictionary<WebSocket, ClientSession>();

        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private BridgeControllerRouter _controllerRouter;
        private bool _disposed;

        public int Port { get; }

        public bool IsRunning => !_disposed && _listener != null && _listener.IsListening;

        public bool HasConnectedClients
        {
            get { return GetConnectedClientCount() > 0; }
        }

        public event EventHandler ClientsChanged;

        public event EventHandler<BridgeClientMessageEventArgs> MessageReceived;

        public event EventHandler<BridgeControllerMessageEventArgs> ControllerMessageReceived;

        public BridgeWebSocketServer(int port = BridgeConstants.DefaultPort)
        {
            Port = port;
        }

        public int GetConnectedClientCount()
        {
            lock (_sync)
            {
                return _clientsByInstanceId.Count;
            }
        }

        public IReadOnlyList<BridgeClientInfo> GetConnectedClients()
        {
            lock (_sync)
            {
                return _clientsByInstanceId
                    .Select(pair => new BridgeClientInfo(pair.Key, pair.Value.Label, pair.Value.ConnectedAtUtc.ToLocalTime()))
                    .OrderBy(client => client.ConnectedAt)
                    .ToList();
            }
        }

        public void Start()
        {
            ThrowIfDisposed();

            if (_listener != null)
                return;

            _cts = new CancellationTokenSource();
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://" + BridgeConstants.DefaultHost + ":" + Port + "/");
            _listener.Start();

            _controllerRouter = new BridgeControllerRouter(this);
            Task.Run(() => AcceptLoopAsync(_cts.Token));
            Task.Run(() => HeartbeatLoopAsync(_cts.Token));
        }

        public void Stop()
        {
            if (_listener == null)
                return;

            _cts?.Cancel();
            _listener.Stop();
            _listener.Close();
            _listener = null;

            _controllerRouter?.Dispose();
            _controllerRouter = null;

            CloseAllClients();
        }

        public async Task SendControllerAsync(string controllerId, string message, CancellationToken cancellationToken = default)
        {
            ClientSession session;
            lock (_sync)
            {
                if (!_controllersById.TryGetValue(controllerId, out session))
                    throw new InvalidOperationException("Bridge controller is not connected: " + controllerId);
            }

            await SendTextAsync(session.Socket, message, cancellationToken).ConfigureAwait(false);
        }

        public async Task SendAsync(string message, CancellationToken cancellationToken = default)
        {
            await SendAsync(message, null, cancellationToken).ConfigureAwait(false);
        }

        public async Task SendAsync(string message, string instanceId, CancellationToken cancellationToken = default)
        {
            List<ClientSession> targets;
            lock (_sync)
            {
                if (string.IsNullOrWhiteSpace(instanceId))
                {
                    targets = _clientsByInstanceId.Values.ToList();
                }
                else if (!_clientsByInstanceId.TryGetValue(instanceId, out var session))
                {
                    throw new InvalidOperationException("Chromium extension instance is not connected: " + instanceId);
                }
                else
                {
                    targets = new List<ClientSession> { session };
                }
            }

            if (targets.Count == 0)
                throw new InvalidOperationException("No chromium extension instances are connected.");

            var failures = new List<Exception>();
            foreach (var target in targets)
            {
                try
                {
                    await SendTextAsync(target.Socket, message, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    failures.Add(ex);
                    RemoveSession(target.Socket, notify: true);
                }
            }

            if (failures.Count == targets.Count)
                throw new InvalidOperationException("Failed to send message to chromium extension.", failures[0]);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Stop();
            _controllerRouter?.Dispose();
            _controllerRouter = null;
            _cts?.Dispose();
        }

        private async Task AcceptLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _listener != null && _listener.IsListening)
            {
                HttpListenerContext context = null;
                try
                {
                    context = await _listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                if (context == null)
                    continue;

                if (!context.Request.IsWebSocketRequest)
                {
                    WriteHealthResponse(context);
                    continue;
                }

                var wsContext = await context.AcceptWebSocketAsync(null).ConfigureAwait(false);
                var session = RegisterPendingSession(wsContext.WebSocket);
                _ = Task.Run(() => ReceiveLoopAsync(session, cancellationToken), cancellationToken);
            }
        }

        private static void WriteHealthResponse(HttpListenerContext context)
        {
            var path = context.Request.Url != null ? context.Request.Url.AbsolutePath.TrimEnd('/') : string.Empty;
            if (string.IsNullOrEmpty(path))
                path = "/";

            if (!path.Equals("/health", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }

            var json = JsonSerializer.Serialize(new Dictionary<string, object> { { "ok", true } });
            var buffer = Encoding.UTF8.GetBytes(json);
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentEncoding = Encoding.UTF8;
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();
        }

        private ClientSession RegisterPendingSession(WebSocket socket)
        {
            var session = new ClientSession(socket);
            lock (_sync)
            {
                _sessionsBySocket[socket] = session;
            }

            return session;
        }

        private async Task ReceiveLoopAsync(ClientSession session, CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];
            var builder = new StringBuilder();
            var helloDeadlineUtc = DateTime.UtcNow.AddSeconds(BridgeConstants.HelloTimeoutSeconds);

            try
            {
                while (!cancellationToken.IsCancellationRequested && session.Socket.State == WebSocketState.Open)
                {
                    builder.Clear();
                    WebSocketReceiveResult result;

                    do
                    {
                        result = await session.Socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken)
                            .ConfigureAwait(false);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await session.Socket.CloseAsync(
                                WebSocketCloseStatus.NormalClosure,
                                string.Empty,
                                CancellationToken.None).ConfigureAwait(false);
                            return;
                        }

                        builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    }
                    while (!result.EndOfMessage);

                    var payload = builder.ToString();
                    session.LastActivityUtc = DateTime.UtcNow;

                    if (!session.IsRegistered)
                    {
                        if (!BridgeMessageParser.TryParseHello(payload, out var instanceId, out var label, out var role))
                        {
                            if (DateTime.UtcNow >= helloDeadlineUtc)
                                return;

                            continue;
                        }

                        if (!TryRegisterSession(session, instanceId, label, role))
                            return;

                        continue;
                    }

                    if (session.Role == BridgeClientRole.Controller)
                    {
                        ControllerMessageReceived?.Invoke(
                            this,
                            new BridgeControllerMessageEventArgs(session.InstanceId, payload));
                        continue;
                    }

                    if (HandleInternalMessage(session, payload))
                        continue;

                    MessageReceived?.Invoke(
                        this,
                        new BridgeClientMessageEventArgs(session.InstanceId, payload));
                }
            }
            catch (WebSocketException)
            {
                // Client disconnected.
            }
            catch (OperationCanceledException)
            {
                // Server is stopping.
            }
            finally
            {
                RemoveSession(session.Socket, notify: true);
            }
        }

        private bool TryRegisterSession(ClientSession session, string instanceId, string label, BridgeClientRole role)
        {
            session.Role = role;
            session.InstanceId = instanceId;
            session.Label = label;
            session.IsRegistered = true;
            session.LastActivityUtc = DateTime.UtcNow;

            if (role == BridgeClientRole.Controller)
            {
                ClientSession previousController = null;
                lock (_sync)
                {
                    if (_controllersById.TryGetValue(instanceId, out previousController) &&
                        !ReferenceEquals(previousController, session))
                    {
                        _controllersById.Remove(instanceId);
                        _sessionsBySocket.Remove(previousController.Socket);
                    }

                    _controllersById[instanceId] = session;
                    _sessionsBySocket[session.Socket] = session;
                }

                if (previousController != null && !ReferenceEquals(previousController, session))
                    CloseSessionSocket(previousController);

                return true;
            }

            ClientSession previousSession = null;

            lock (_sync)
            {
                if (_clientsByInstanceId.TryGetValue(instanceId, out previousSession) &&
                    !ReferenceEquals(previousSession, session))
                {
                    _clientsByInstanceId.Remove(instanceId);
                    _sessionsBySocket.Remove(previousSession.Socket);
                }

                _clientsByInstanceId[instanceId] = session;
                _sessionsBySocket[session.Socket] = session;
            }

            if (previousSession != null && !ReferenceEquals(previousSession, session))
                CloseSessionSocket(previousSession);

            ClientsChanged?.Invoke(this, EventArgs.Empty);
            NotifyAttachedControllersClientsChanged();
            return true;
        }

        private void NotifyAttachedControllersClientsChanged()
        {
            List<ClientSession> controllers;
            lock (_sync)
            {
                controllers = _controllersById.Values.ToList();
            }

            if (controllers.Count == 0)
                return;

            const string payload = "{\"type\":\"clientsChanged\"}";
            foreach (var controller in controllers)
            {
                if (controller.Socket != null && controller.Socket.State == WebSocketState.Open)
                    _ = SendTextAsync(controller.Socket, payload, CancellationToken.None);
            }
        }

        private bool HandleInternalMessage(ClientSession session, string payload)
        {
            if (!BridgeMessageParser.TryGetType(payload, out var type))
                return false;

            if (string.Equals(type, "pong", StringComparison.Ordinal) ||
                string.Equals(type, "ping", StringComparison.Ordinal))
            {
                session.LastActivityUtc = DateTime.UtcNow;

                if (string.Equals(type, "ping", StringComparison.Ordinal))
                    _ = SendTextAsync(session.Socket, "{\"type\":\"pong\"}", CancellationToken.None);

                return true;
            }

            return false;
        }

        private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(BridgeConstants.HeartbeatIntervalSeconds),
                        cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                await SendPingToAllAsync().ConfigureAwait(false);
                PruneStaleClients();
            }
        }

        private async Task SendPingToAllAsync()
        {
            List<ClientSession> sessions;
            lock (_sync)
            {
                sessions = _clientsByInstanceId.Values.ToList();
            }

            foreach (var session in sessions)
            {
                try
                {
                    await SendTextAsync(session.Socket, "{\"type\":\"ping\"}", CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch
                {
                    RemoveSession(session.Socket, notify: true);
                }
            }
        }

        private void PruneStaleClients()
        {
            var staleBeforeUtc = DateTime.UtcNow.AddSeconds(-BridgeConstants.HeartbeatTimeoutSeconds);
            List<ClientSession> staleSessions;

            lock (_sync)
            {
                staleSessions = _clientsByInstanceId.Values
                    .Where(session => session.LastActivityUtc < staleBeforeUtc)
                    .ToList();
            }

            foreach (var session in staleSessions)
                RemoveSession(session.Socket, notify: true);
        }

        private static async Task SendTextAsync(WebSocket socket, string message, CancellationToken cancellationToken)
        {
            if (socket == null || socket.State != WebSocketState.Open)
                throw new InvalidOperationException("WebSocket is not open.");

            var buffer = Encoding.UTF8.GetBytes(message);
            await socket.SendAsync(
                new ArraySegment<byte>(buffer),
                WebSocketMessageType.Text,
                true,
                cancellationToken).ConfigureAwait(false);
        }

        private void RemoveSession(WebSocket socket, bool notify)
        {
            ClientSession session;
            var removedRegisteredClient = false;

            lock (_sync)
            {
                if (!_sessionsBySocket.TryGetValue(socket, out session))
                    return;

                _sessionsBySocket.Remove(socket);

                if (session.IsRegistered &&
                    session.InstanceId != null &&
                    session.Role == BridgeClientRole.Controller &&
                    _controllersById.TryGetValue(session.InstanceId, out var currentController) &&
                    ReferenceEquals(currentController, session))
                {
                    _controllersById.Remove(session.InstanceId);
                }
                else if (session.IsRegistered &&
                    session.InstanceId != null &&
                    _clientsByInstanceId.TryGetValue(session.InstanceId, out var current) &&
                    ReferenceEquals(current, session))
                {
                    _clientsByInstanceId.Remove(session.InstanceId);
                    removedRegisteredClient = true;
                }
            }

            CloseSessionSocket(session);

            if (notify && removedRegisteredClient)
            {
                ClientsChanged?.Invoke(this, EventArgs.Empty);
                NotifyAttachedControllersClientsChanged();
            }
        }

        private void CloseSessionSocket(ClientSession session)
        {
            if (session?.Socket == null)
                return;

            try
            {
                if (session.Socket.State == WebSocketState.Open)
                {
                    session.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None)
                        .ContinueWith(_ => DisposeSocket(session.Socket));
                }
                else
                {
                    DisposeSocket(session.Socket);
                }
            }
            catch
            {
                DisposeSocket(session.Socket);
            }
        }

        private static void DisposeSocket(WebSocket socket)
        {
            try
            {
                socket.Dispose();
            }
            catch
            {
                // Ignore dispose errors.
            }
        }

        private void CloseAllClients()
        {
            List<ClientSession> sessions;
            lock (_sync)
            {
                sessions = _sessionsBySocket.Values.ToList();
                _sessionsBySocket.Clear();
                _clientsByInstanceId.Clear();
                _controllersById.Clear();
            }

            foreach (var session in sessions)
                CloseSessionSocket(session);

            ClientsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BridgeWebSocketServer));
        }
    }
}
