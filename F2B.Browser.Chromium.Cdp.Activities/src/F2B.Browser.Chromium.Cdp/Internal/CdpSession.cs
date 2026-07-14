using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal static class CdpSession
    {
        public static void Navigate(string webSocketDebuggerUrl, string url, TimeSpan timeout)
        {
            Run(webSocketDebuggerUrl, timeout, client =>
            {
                SendCommand(client, 1, "Page.enable", null);
                SendCommand(client, 2, "Page.navigate", new { url });
            });
        }

        public static void ClickStaySignedOut(string webSocketDebuggerUrl, TimeSpan timeout)
        {
            Run(webSocketDebuggerUrl, timeout, client =>
            {
                SendCommand(client, 1, "Runtime.enable", null);
                SendCommand(client, 2, "Runtime.evaluate", new
                {
                    expression = @"
(function () {
  var labels = [
    '保持未登录状�?, 'Stay signed out', 'Use without an account', 'Continue without an account',
    'Not now', 'Skip', '稍后', '跳过', '以后再说', 'Get started', 'Continue'
  ];
  var buttons = document.querySelectorAll('button, cr-button');
  for (var i = 0; i < buttons.length; i++) {
    var text = (buttons[i].innerText || buttons[i].textContent || '').trim();
    for (var j = 0; j < labels.length; j++) {
      if (text.indexOf(labels[j]) >= 0) {
        buttons[i].click();
        return true;
      }
    }
  }
  return false;
})()",
                    returnByValue = true
                });
            });
        }

        private static void Run(string webSocketDebuggerUrl, TimeSpan timeout, Action<ClientWebSocket> action)
        {
            using (var cancellation = new CancellationTokenSource(timeout))
            {
                RunAsync(webSocketDebuggerUrl, action, cancellation.Token).GetAwaiter().GetResult();
            }
        }

        private static async Task RunAsync(
            string webSocketDebuggerUrl,
            Action<ClientWebSocket> action,
            CancellationToken cancellationToken)
        {
            using (var client = new ClientWebSocket())
            {
                await client.ConnectAsync(new Uri(webSocketDebuggerUrl), cancellationToken).ConfigureAwait(false);
                action(client);

                // Allow commands to complete before closing the socket.
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            }
        }

        private static void SendCommand(ClientWebSocket client, int id, string method, object parameters)
        {
            var serializer = new CdpJsonSerializer();
            var payload = parameters == null
                ? serializer.Serialize(new { id, method })
                : serializer.Serialize(new { id, method, @params = parameters });

            var bytes = Encoding.UTF8.GetBytes(payload);
            client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            ReceiveOneMessage(client);
        }

        private static void ReceiveOneMessage(ClientWebSocket client)
        {
            var buffer = new byte[8192];
            var segment = new ArraySegment<byte>(buffer);
            var builder = new StringBuilder();

            while (client.State == WebSocketState.Open)
            {
                var result = client.ReceiveAsync(segment, CancellationToken.None).GetAwaiter().GetResult();
                builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                if (result.EndOfMessage)
                {
                    break;
                }
            }
        }
    }
}
