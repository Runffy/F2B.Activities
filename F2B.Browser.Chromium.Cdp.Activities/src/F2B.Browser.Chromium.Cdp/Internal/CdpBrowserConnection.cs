using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using F2B.Browser.Chromium.Cdp.Exceptions;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal sealed class CdpBrowserConnection : IDisposable
    {
        private readonly string _webSocketUrl;
        private readonly TimeSpan _commandTimeout;
        private int _messageId;

        public CdpBrowserConnection(string webSocketUrl, TimeSpan? commandTimeout = null)
        {
            if (string.IsNullOrWhiteSpace(webSocketUrl))
            {
                throw new ArgumentNullException("webSocketUrl");
            }

            _webSocketUrl = webSocketUrl;
            _commandTimeout = commandTimeout ?? TimeSpan.FromSeconds(30);
        }

        public Dictionary<string, object> SendCommand(string method, Dictionary<string, object> parameters = null)
        {
            using (var cancellation = new CancellationTokenSource(_commandTimeout))
            {
                return SendCommandAsync(method, parameters, cancellation.Token).GetAwaiter().GetResult();
            }
        }

        public void ActivateTarget(string targetId)
        {
            SendCommand("Target.activateTarget", new Dictionary<string, object>
            {
                { "targetId", targetId }
            });
        }

        public void CloseTarget(string targetId)
        {
            SendCommand("Target.closeTarget", new Dictionary<string, object>
            {
                { "targetId", targetId }
            });
        }

        public void CloseBrowser()
        {
            SendCommand("Browser.close");
        }

        public string CreateTarget(
            string url,
            bool newWindow,
            bool background,
            string browserContextId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "url", url ?? string.Empty }
            };

            if (newWindow)
            {
                parameters["newWindow"] = true;
            }

            if (background)
            {
                parameters["background"] = true;
            }

            if (!string.IsNullOrWhiteSpace(browserContextId))
            {
                parameters["browserContextId"] = browserContextId;
            }

            var result = SendCommand("Target.createTarget", parameters);
            return GetRequiredString(result, "targetId");
        }

        public string CreateBrowserContext()
        {
            var result = SendCommand("Target.createBrowserContext");
            return GetRequiredString(result, "browserContextId");
        }

        public Dictionary<string, object> GetWindowForTarget(string targetId)
        {
            return SendCommand("Browser.getWindowForTarget", new Dictionary<string, object>
            {
                { "targetId", targetId }
            });
        }

        public void SetWindowBounds(int windowId, Dictionary<string, object> bounds)
        {
            SendCommand("Browser.setWindowBounds", new Dictionary<string, object>
            {
                { "windowId", windowId },
                { "bounds", bounds }
            });
        }

        public void Dispose()
        {
        }

        private async Task<Dictionary<string, object>> SendCommandAsync(
            string method,
            Dictionary<string, object> parameters,
            CancellationToken cancellationToken)
        {
            using (var client = new ClientWebSocket())
            {
                await client.ConnectAsync(new Uri(_webSocketUrl), cancellationToken).ConfigureAwait(false);

                var messageId = Interlocked.Increment(ref _messageId);
                var payload = BuildPayload(messageId, method, parameters);
                var bytes = Encoding.UTF8.GetBytes(payload);

                await client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken)
                    .ConfigureAwait(false);

                var responseText = await ReceiveMessageAsync(client, cancellationToken).ConfigureAwait(false);
                return ParseResult(responseText, messageId);
            }
        }

        private static string BuildPayload(int id, string method, Dictionary<string, object> parameters)
        {
            var serializer = new CdpJsonSerializer();
            if (parameters == null || parameters.Count == 0)
            {
                return serializer.Serialize(new { id, method });
            }

            return serializer.Serialize(new { id, method, @params = parameters });
        }

        private static async Task<string> ReceiveMessageAsync(ClientWebSocket client, CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];
            var builder = new StringBuilder();

            while (client.State == WebSocketState.Open)
            {
                var segment = new ArraySegment<byte>(buffer);
                var result = await client.ReceiveAsync(segment, cancellationToken).ConfigureAwait(false);
                builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                if (result.EndOfMessage)
                {
                    break;
                }
            }

            return builder.ToString();
        }

        private static Dictionary<string, object> ParseResult(string responseText, int messageId)
        {
            var serializer = new CdpJsonSerializer();
            var response = serializer.DeserializeObject(responseText) as Dictionary<string, object>;
            if (response == null)
            {
                throw new BrowserException("Invalid CDP response.");
            }

            object errorValue;
            if (response.TryGetValue("error", out errorValue) && errorValue != null)
            {
                throw new BrowserException(string.Format("CDP command failed: {0}", errorValue));
            }

            object idValue;
            if (response.TryGetValue("id", out idValue) && Convert.ToInt32(idValue) != messageId)
            {
                throw new BrowserException("CDP response id mismatch.");
            }

            object resultValue;
            if (!response.TryGetValue("result", out resultValue) || resultValue == null)
            {
                return new Dictionary<string, object>();
            }

            return resultValue as Dictionary<string, object> ?? new Dictionary<string, object>();
        }

        private static string GetRequiredString(Dictionary<string, object> result, string key)
        {
            object value;
            if (result.TryGetValue(key, out value) && value != null)
            {
                return value.ToString();
            }

            throw new BrowserException(string.Format("CDP response missing '{0}'.", key));
        }
    }
}
