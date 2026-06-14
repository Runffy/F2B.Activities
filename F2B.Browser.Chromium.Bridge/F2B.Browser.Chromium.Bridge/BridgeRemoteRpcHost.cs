using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace F2B.Browser.Chromium.Bridge
{
    /// <summary>
    /// WebSocket controller client that attaches to an existing Bridge server (e.g. OpenRPA host).
    /// </summary>
    public sealed class BridgeRemoteRpcHost : IBridgeRpcChannel, IDisposable
    {
        private readonly object _sync = new object();
        private readonly Dictionary<string, TaskCompletionSource<BridgeRpcResponse>> _pending =
            new Dictionary<string, TaskCompletionSource<BridgeRpcResponse>>();
        private readonly int _port;
        private readonly string _controllerId;

        private ClientWebSocket _socket;
        private CancellationTokenSource _receiveCts;
        private Task _receiveTask;
        private bool _disposed;

        private BridgeRemoteRpcHost(int port, string controllerId)
        {
            _port = port;
            _controllerId = controllerId;
        }

        public event EventHandler ClientsChanged;

        public static BridgeRemoteRpcHost Connect(int port = BridgeConstants.DefaultPort, string controllerLabel = "F2B Bridge Controller")
        {
            if (!BridgeServerProbe.IsBridgeListening(port))
            {
                throw new InvalidOperationException(
                    "Bridge server is not listening on port " + port + ". Start OpenRPA, Inspector, or another Bridge host first.");
            }

            var prefix = controllerLabel != null &&
                         controllerLabel.IndexOf("inspector", StringComparison.OrdinalIgnoreCase) >= 0
                ? "inspector"
                : "openrpa";
            var host = new BridgeRemoteRpcHost(port, prefix + "-" + Guid.NewGuid().ToString("N").Substring(0, 12));
            host.ConnectInternal(controllerLabel ?? "F2B Bridge Controller");
            return host;
        }

        public bool IsConnected =>
            !_disposed && _socket != null && _socket.State == WebSocketState.Open;

        public IReadOnlyList<BridgeClientInfo> ListExtensionClients()
        {
            var response = InvokeAsync(
                    "bridge.listClients",
                    string.Empty,
                    null,
                    5000)
                .GetAwaiter()
                .GetResult();

            BridgeClientErrors.EnsureSuccess(response, "bridge.listClients");

            var items = BridgeJson.GetArray(response.Data, "clients");
            return items
                .OfType<Dictionary<string, object>>()
                .Select(item => new BridgeClientInfo(
                    BridgeJson.GetString(item, "instanceId"),
                    BridgeJson.GetString(item, "label"),
                    DateTime.Now))
                .Where(item => !string.IsNullOrWhiteSpace(item.InstanceId))
                .ToList();
        }

        public async Task<BridgeRpcResponse> InvokeAsync(
            string action,
            string instanceId,
            IDictionary<string, object> parameters = null,
            int timeoutMs = 15000,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var requestId = Guid.NewGuid().ToString("N");
            var payload = new Dictionary<string, object>
            {
                { "type", "command" },
                { "id", requestId },
                { "action", action }
            };

            if (!string.IsNullOrWhiteSpace(instanceId))
                payload["targetInstanceId"] = instanceId;

            if (parameters != null)
            {
                foreach (var pair in parameters)
                    payload[pair.Key] = pair.Value;
            }

            var tcs = new TaskCompletionSource<BridgeRpcResponse>();
            lock (_sync)
            {
                _pending[requestId] = tcs;
            }

            try
            {
                await SendTextAsync(BridgeJson.Serialize(payload), cancellationToken).ConfigureAwait(false);

                using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    timeoutCts.CancelAfter(timeoutMs);
                    var delayTask = Task.Delay(Timeout.Infinite, timeoutCts.Token);
                    var completed = await Task.WhenAny(tcs.Task, delayTask).ConfigureAwait(false);
                    if (completed != tcs.Task)
                        throw new TimeoutException("Bridge controller command timed out: " + action);
                }

                return await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                lock (_sync)
                {
                    _pending.Remove(requestId);
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _receiveCts?.Cancel();

            try
            {
                _receiveTask?.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
            }

            if (_socket != null)
            {
                try
                {
                    if (_socket.State == WebSocketState.Open)
                    {
                        _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None)
                            .GetAwaiter()
                            .GetResult();
                    }
                }
                catch
                {
                }

                _socket.Dispose();
                _socket = null;
            }

            _receiveCts?.Dispose();

            lock (_sync)
            {
                foreach (var pending in _pending.Values)
                    pending.TrySetCanceled();
                _pending.Clear();
            }
        }

        private void ConnectInternal(string label)
        {
            _socket = new ClientWebSocket();
            _receiveCts = new CancellationTokenSource();
            var uri = new Uri("ws://" + BridgeConstants.DefaultHost + ":" + _port + "/");
            _socket.ConnectAsync(uri, _receiveCts.Token).GetAwaiter().GetResult();

            var hello = BridgeJson.Serialize(new Dictionary<string, object>
            {
                { "type", "hello" },
                { "role", "controller" },
                { "instanceId", _controllerId },
                { "label", label ?? "F2B Bridge Controller" }
            });

            SendTextAsync(hello, CancellationToken.None).GetAwaiter().GetResult();
            _receiveTask = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token));
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];
            var builder = new StringBuilder();

            try
            {
                while (!cancellationToken.IsCancellationRequested &&
                       _socket != null &&
                       _socket.State == WebSocketState.Open)
                {
                    builder.Clear();
                    WebSocketReceiveResult result;

                    do
                    {
                        result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken)
                            .ConfigureAwait(false);

                        if (result.MessageType == WebSocketMessageType.Close)
                            return;

                        builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    }
                    while (!result.EndOfMessage);

                    HandleMessage(builder.ToString());
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (WebSocketException)
            {
            }
        }

        private void HandleMessage(string payload)
        {
            var message = BridgeJson.ParseObject(payload);
            var type = BridgeJson.GetString(message, "type");
            if (string.Equals(type, "clientsChanged", StringComparison.OrdinalIgnoreCase))
            {
                ClientsChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            if (!string.Equals(type, "result", StringComparison.OrdinalIgnoreCase))
                return;

            var requestId = BridgeJson.GetString(message, "id");
            if (string.IsNullOrWhiteSpace(requestId))
                return;

            TaskCompletionSource<BridgeRpcResponse> tcs;
            lock (_sync)
            {
                if (!_pending.TryGetValue(requestId, out tcs))
                    return;
            }

            tcs.TrySetResult(new BridgeRpcResponse
            {
                Success = BridgeJson.GetBool(message, "success"),
                Error = BridgeJson.GetString(message, "error"),
                Data = BridgeJson.GetObject(message, "data")
            });
        }

        private async Task SendTextAsync(string message, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (_socket == null || _socket.State != WebSocketState.Open)
                throw new InvalidOperationException("Bridge controller socket is not connected.");

            var bytes = Encoding.UTF8.GetBytes(message ?? string.Empty);
            await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken)
                .ConfigureAwait(false);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BridgeRemoteRpcHost));
        }
    }
}
