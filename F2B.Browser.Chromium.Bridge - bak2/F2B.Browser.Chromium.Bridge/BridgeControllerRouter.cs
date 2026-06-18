using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace F2B.Browser.Chromium.Bridge
{
    /// <summary>
    /// Routes Inspector/controller RPC requests to connected Chrome extension instances.
    /// </summary>
    public sealed class BridgeControllerRouter : IDisposable
    {
        private readonly BridgeWebSocketServer _server;
        private readonly BridgeRpcHost _rpcHost;

        public BridgeControllerRouter(BridgeWebSocketServer server)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _rpcHost = new BridgeRpcHost(server);
            _server.ControllerMessageReceived += OnControllerMessageReceived;
        }

        public void Dispose()
        {
            _server.ControllerMessageReceived -= OnControllerMessageReceived;
            _rpcHost.Dispose();
        }

        private async void OnControllerMessageReceived(object sender, BridgeControllerMessageEventArgs e)
        {
            if (e == null || string.IsNullOrWhiteSpace(e.Message))
                return;

            try
            {
                await HandleControllerMessageAsync(e).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await _server.SendControllerAsync(
                    e.ControllerId,
                    BridgeJson.Serialize(new Dictionary<string, object>
                    {
                        { "type", "result" },
                        { "id", ExtractRequestId(e.Message) ?? string.Empty },
                        { "success", false },
                        { "error", ex.Message ?? "Controller command failed." },
                        { "data", new Dictionary<string, object>() }
                    })).ConfigureAwait(false);
            }
        }

        private async Task HandleControllerMessageAsync(BridgeControllerMessageEventArgs e)
        {
            var message = BridgeJson.ParseObject(e.Message);
            var type = BridgeJson.GetString(message, "type");
            if (!string.Equals(type, "command", StringComparison.OrdinalIgnoreCase))
                return;

            var requestId = BridgeJson.GetString(message, "id") ?? Guid.NewGuid().ToString("N");
            var action = BridgeJson.GetString(message, "action");
            if (string.IsNullOrWhiteSpace(action))
                throw new InvalidOperationException("Controller command is missing action.");

            if (string.Equals(action, "bridge.listClients", StringComparison.OrdinalIgnoreCase))
            {
                var clients = _server.GetConnectedClients()
                    .Select(client => (object)new Dictionary<string, object>
                    {
                        { "instanceId", client.InstanceId },
                        { "label", client.DisplayName ?? string.Empty }
                    })
                    .ToArray();

                await SendControllerResultAsync(
                    e.ControllerId,
                    requestId,
                    true,
                    new Dictionary<string, object> { { "clients", clients } },
                    null).ConfigureAwait(false);
                return;
            }

            var targetInstanceId = BridgeJson.GetString(message, "targetInstanceId");
            if (string.IsNullOrWhiteSpace(targetInstanceId))
            {
                var clients = _server.GetConnectedClients();
                if (clients.Count == 1)
                    targetInstanceId = clients[0].InstanceId;
                else if (clients.Count == 0)
                    throw new InvalidOperationException("No Chrome extension is connected to Bridge.");
                else
                    throw new InvalidOperationException("Multiple extensions connected. Specify targetInstanceId.");
            }

            var timeoutMs = BridgeJson.GetInt(message, "timeout", 15000);
            if (timeoutMs <= 0)
                timeoutMs = 15000;

            var parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in message)
            {
                if (string.Equals(pair.Key, "type", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(pair.Key, "id", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(pair.Key, "action", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(pair.Key, "targetInstanceId", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                parameters[pair.Key] = pair.Value;
            }

            var response = await _rpcHost.InvokeAsync(
                action,
                targetInstanceId,
                parameters,
                timeoutMs).ConfigureAwait(false);

            await SendControllerResultAsync(
                e.ControllerId,
                requestId,
                response.Success,
                response.Data ?? new Dictionary<string, object>(),
                response.Error).ConfigureAwait(false);
        }

        private Task SendControllerResultAsync(
            string controllerId,
            string requestId,
            bool success,
            Dictionary<string, object> data,
            string error)
        {
            var payload = new Dictionary<string, object>
            {
                { "type", "result" },
                { "id", requestId ?? string.Empty },
                { "success", success },
                { "data", data ?? new Dictionary<string, object>() }
            };

            if (!string.IsNullOrWhiteSpace(error))
                payload["error"] = error;

            return _server.SendControllerAsync(controllerId, BridgeJson.Serialize(payload));
        }

        private static string ExtractRequestId(string json)
        {
            var message = BridgeJson.ParseObject(json);
            return BridgeJson.GetString(message, "id");
        }
    }
}
