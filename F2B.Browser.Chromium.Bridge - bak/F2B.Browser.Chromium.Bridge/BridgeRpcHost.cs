using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using F2B.Browser.Chromium.Bridge.Selectors;

namespace F2B.Browser.Chromium.Bridge
{
    public sealed class BridgeRpcHost : IBridgeRpcChannel, IDisposable
    {
        private readonly BridgeWebSocketServer _server;
        private readonly object _sync = new object();
        private readonly Dictionary<string, TaskCompletionSource<BridgeRpcResponse>> _pending =
            new Dictionary<string, TaskCompletionSource<BridgeRpcResponse>>();
        private readonly Dictionary<string, long> _requestStartedAtUtc =
            new Dictionary<string, long>();

        private static string ShortRequestId(string requestId)
        {
            if (string.IsNullOrEmpty(requestId))
                return "unknown";

            return requestId.Length <= 8 ? requestId : requestId.Substring(0, 8);
        }

        private static long ElapsedMs(long startedAtUtcTicks)
        {
            return (Stopwatch.GetTimestamp() - startedAtUtcTicks) * 1000 / Stopwatch.Frequency;
        }

        public BridgeRpcHost(BridgeWebSocketServer server)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _server.MessageReceived += OnMessageReceived;
        }

        public async Task<BridgeRpcResponse> InvokeAsync(
            string action,
            string instanceId,
            IDictionary<string, object> parameters = null,
            int timeoutMs = 15000,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
                throw new ArgumentException("Extension instanceId is required.", nameof(instanceId));

            var requestId = Guid.NewGuid().ToString("N");
            var payload = new Dictionary<string, object>
            {
                { "type", "command" },
                { "id", requestId },
                { "action", action }
            };

            if (parameters != null)
            {
                foreach (var pair in parameters)
                    payload[pair.Key] = pair.Value;
            }

            var tcs = new TaskCompletionSource<BridgeRpcResponse>();
            lock (_sync)
            {
                _pending[requestId] = tcs;
                _requestStartedAtUtc[requestId] = Stopwatch.GetTimestamp();
            }

            BridgeDiagnostics.Trace("[HOST:" + ShortRequestId(requestId) + "] send " + action);

            try
            {
                await _server.SendAsync(BridgeJson.Serialize(payload), instanceId, cancellationToken)
                    .ConfigureAwait(false);

                BridgeDiagnostics.Trace("[HOST:" + ShortRequestId(requestId) + "] waiting response +0ms");

                using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    timeoutCts.CancelAfter(timeoutMs);
                    var delayTask = Task.Delay(Timeout.Infinite, timeoutCts.Token);
                    var completed = await Task.WhenAny(tcs.Task, delayTask).ConfigureAwait(false);
                    if (completed != tcs.Task)
                    {
                        long startedAtUtc;
                        lock (_sync)
                        {
                            _requestStartedAtUtc.TryGetValue(requestId, out startedAtUtc);
                        }

                        BridgeDiagnostics.Trace(
                            "[HOST:" + ShortRequestId(requestId) + "] timeout at +" + ElapsedMs(startedAtUtc) + "ms, grace 500ms");

                        var late = await Task.WhenAny(tcs.Task, Task.Delay(500, cancellationToken)).ConfigureAwait(false);
                        if (late == tcs.Task)
                        {
                            BridgeDiagnostics.Trace("[HOST:" + ShortRequestId(requestId) + "] late response accepted");
                            return await tcs.Task.ConfigureAwait(false);
                        }

                        BridgeDiagnostics.Trace("[HOST:" + ShortRequestId(requestId) + "] timeout confirmed, no pending match");
                        throw new TimeoutException("Bridge command timed out: " + action);
                    }
                }

                return await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                lock (_sync)
                {
                    _pending.Remove(requestId);
                    _requestStartedAtUtc.Remove(requestId);
                }
            }
        }

        public static object[] BuildSelectorLevelsPayload(IEnumerable<SelectorLevel> levels)
        {
            if (levels == null)
                return new object[0];

            return levels.Select(level => (object)new Dictionary<string, object>
            {
                {
                    "tagName", level.TagName
                },
                {
                    "properties", level.Properties
                        .Where(property => property.IsSelected)
                        .Select(property => (object)new Dictionary<string, object>
                        {
                            { "name", property.Name },
                            { "value", property.Value ?? string.Empty },
                            { "isRegex", property.IsRegex }
                        })
                        .ToArray()
                }
            }).ToArray();
        }

        public static Dictionary<string, object> WithScopeForTab(
            SelectorScope scope,
            BwTab tab,
            IDictionary<string, object> parameters = null)
        {
            var payload = parameters != null
                ? new Dictionary<string, object>(parameters)
                : new Dictionary<string, object>();

            if (tab != null)
                payload["tabId"] = tab.TabId;

            payload["frameSelectorLevels"] = BuildSelectorLevelsPayload(scope.FrameLevels);
            payload["selectorLevels"] = BuildSelectorLevelsPayload(scope.ElementLevels);
            return payload;
        }

        public static Dictionary<string, object> WithSelectorXml(string selectorXml, IDictionary<string, object> parameters = null)
        {
            var scope = SelectorXmlSerializer.SplitScope(selectorXml);
            var payload = parameters != null
                ? new Dictionary<string, object>(parameters)
                : new Dictionary<string, object>();

            payload["selectorXml"] = selectorXml ?? string.Empty;
            payload["tabSelectorLevels"] = BuildSelectorLevelsPayload(
                scope.TabLevel == null ? null : new[] { scope.TabLevel });
            payload["frameSelectorLevels"] = BuildSelectorLevelsPayload(scope.FrameLevels);
            payload["selectorLevels"] = BuildSelectorLevelsPayload(scope.ElementLevels);
            return payload;
        }

        public void Dispose()
        {
            _server.MessageReceived -= OnMessageReceived;
            lock (_sync)
            {
                foreach (var pending in _pending.Values)
                    pending.TrySetCanceled();
                _pending.Clear();
            }
        }

        private void OnMessageReceived(object sender, BridgeClientMessageEventArgs e)
        {
            var message = BridgeJson.ParseObject(e.Message);
            if (!message.TryGetValue("type", out var typeObj))
                return;

            var type = Convert.ToString(typeObj);
            if (!string.Equals(type, "result", StringComparison.OrdinalIgnoreCase))
                return;

            if (!message.TryGetValue("id", out var idObj))
                return;

            var requestId = Convert.ToString(idObj);
            TaskCompletionSource<BridgeRpcResponse> tcs;
            long startedAtUtc = 0;
            var hasStartedAt = false;
            lock (_sync)
            {
                if (!_pending.TryGetValue(requestId, out tcs))
                {
                    BridgeDiagnostics.Trace("[HOST:" + ShortRequestId(requestId) + "] orphan response (pending removed)");
                    return;
                }

                hasStartedAt = _requestStartedAtUtc.TryGetValue(requestId, out startedAtUtc);
            }

            var response = new BridgeRpcResponse
            {
                Success = BridgeJson.GetBool(message, "success"),
                Error = BridgeJson.GetString(message, "error"),
                Data = BridgeJson.GetObject(message, "data")
            };

            if (hasStartedAt)
            {
                BridgeDiagnostics.Trace(
                    "[HOST:" + ShortRequestId(requestId) + "] response +" + ElapsedMs(startedAtUtc) + "ms success=" + response.Success);
            }

            tcs.TrySetResult(response);
        }
    }
}
