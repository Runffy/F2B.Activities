using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using F2B.Browser.Chromium.Bridge;

namespace Bridge.Demo.App
{
    public partial class MainWindow : Window
    {
        private sealed class SendTargetItem
        {
            public SendTargetItem(string instanceId, string displayName)
            {
                InstanceId = instanceId;
                DisplayName = displayName;
            }

            public string InstanceId { get; }

            public string DisplayName { get; }
        }

        private readonly BridgeWebSocketServer _server;
        private BridgeSyncClient _bridgeClient;
        private string _bridgeInstanceId;

        public MainWindow()
        {
            InitializeComponent();

            _server = new BridgeWebSocketServer();
            _server.ClientsChanged += OnClientsChanged;
            _server.MessageReceived += OnMessageReceived;
            BridgeDiagnostics.Log = AppendLogUiOnDispatcher;

            lblServerInfo.Text =
                "WebSocket: ws://" + BridgeConstants.DefaultHost + ":" + BridgeConstants.DefaultPort + "/\r\n" +
                "Log file: " + BridgeFileLog.LogFilePath + "\r\n" +
                "Each browser profile keeps its own instanceId and reconnects independently.\r\n" +
                "Load extension folder: extension (under F2B.Browser.Chromium.Bridge)";

            try
            {
                _server.Start();
                AppendLog("Bridge server started.");
                AppendLog("Log file: " + BridgeFileLog.LogFilePath);
                RefreshClientUi();
            }
            catch (Exception ex)
            {
                lblExtensionStatus.Text = "bridge server failed to start";
                AppendLog("Failed to start server: " + ex.Message);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            BridgeDiagnostics.Log = null;
            _bridgeClient?.Dispose();
            _bridgeClient = null;
            _server.ClientsChanged -= OnClientsChanged;
            _server.MessageReceived -= OnMessageReceived;
            _server.Dispose();
            base.OnClosed(e);
        }

        private void OnClientsChanged(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(RefreshClientUi));
        }

        private void OnMessageReceived(object sender, BridgeClientMessageEventArgs e)
        {
            if (TryFormatTraceMessage(e.Message, out var traceLine))
            {
                PostLog(traceLine);
                return;
            }

            PostLog("[" + e.InstanceId + "] " + e.Message);

            if (e.Message.IndexOf("\"type\":\"result\"", StringComparison.Ordinal) >= 0 &&
                e.Message.IndexOf("\"action\":\"alert\"", StringComparison.Ordinal) >= 0)
            {
                if (e.Message.IndexOf("\"success\":true", StringComparison.Ordinal) >= 0)
                    PostLog("Alert succeeded on instance " + e.InstanceId + ".");
                else
                    PostLog("Alert failed on instance " + e.InstanceId + ". Check browser page and extension console.");
            }
        }

        private static bool TryFormatTraceMessage(string message, out string traceLine)
        {
            traceLine = null;

            if (string.IsNullOrEmpty(message) ||
                message.IndexOf("\"type\":\"trace\"", StringComparison.Ordinal) < 0)
            {
                return false;
            }

            var step = ExtractJsonString(message, "step");
            var elapsed = ExtractJsonNumber(message, "elapsedMs");
            var id = ExtractJsonString(message, "id");
            var shortId = string.IsNullOrEmpty(id)
                ? "unknown"
                : (id.Length <= 8 ? id : id.Substring(0, 8));

            traceLine = "TRACE [ext:" + shortId + "] " + step + " +" + elapsed + "ms";
            return true;
        }

        private static string ExtractJsonString(string json, string key)
        {
            var token = "\"" + key + "\":\"";
            var start = json.IndexOf(token, StringComparison.Ordinal);
            if (start < 0)
                return string.Empty;

            start += token.Length;
            var end = json.IndexOf('"', start);
            return end < 0 ? string.Empty : json.Substring(start, end - start);
        }

        private static string ExtractJsonNumber(string json, string key)
        {
            var token = "\"" + key + "\":";
            var start = json.IndexOf(token, StringComparison.Ordinal);
            if (start < 0)
                return "?";

            start += token.Length;
            var end = start;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-'))
                end++;

            return end <= start ? "?" : json.Substring(start, end - start);
        }

        private void RefreshClientUi()
        {
            var clients = _server.GetConnectedClients();
            var count = clients.Count;

            lstInstances.ItemsSource = clients.ToList();

            var targets = new List<SendTargetItem>
            {
                new SendTargetItem(null, "All connected instances")
            };
            targets.AddRange(clients.Select(client => new SendTargetItem(client.InstanceId, client.DisplayName)));

            var previousTarget = cboSendTarget.SelectedValue as string;
            cboSendTarget.ItemsSource = targets;
            cboSendTarget.SelectedValue = targets.Any(item => item.InstanceId == previousTarget)
                ? previousTarget
                : null;

            if (count == 0)
            {
                lblExtensionStatus.Text = "chromium extension offline";
                lblExtensionStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xC0, 0x39, 0x2B));
                btnSendAlert.IsEnabled = false;
                btnGetBaiduButtonText.IsEnabled = false;
                btnClickBaiduButton.IsEnabled = false;
                _bridgeClient?.Dispose();
                _bridgeClient = null;
                _bridgeInstanceId = null;
                return;
            }

            lblExtensionStatus.Text = count == 1
                ? "chromium extension online"
                : "chromium extension online (" + count + " instances)";
            lblExtensionStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60));
            btnSendAlert.IsEnabled = true;
            btnGetBaiduButtonText.IsEnabled = true;
            btnClickBaiduButton.IsEnabled = true;
        }

        private BridgeSyncClient GetOrCreateClient()
        {
            var clients = _server.GetConnectedClients();
            if (clients.Count == 0)
                throw new InvalidOperationException("No extension instance is connected.");

            var targetId = cboSendTarget.SelectedValue as string;
            if (string.IsNullOrWhiteSpace(targetId))
                targetId = clients[0].InstanceId;

            if (_bridgeClient == null || !string.Equals(_bridgeInstanceId, targetId, StringComparison.Ordinal))
            {
                _bridgeClient?.Dispose();
                _bridgeClient = new BridgeSyncClient(_server, targetId);
                _bridgeInstanceId = targetId;
            }

            return _bridgeClient;
        }

        private async void BtnGetBaiduButtonText_OnClick(object sender, RoutedEventArgs e)
        {
            btnGetBaiduButtonText.IsEnabled = false;
            try
            {
                AppendLog("GetText clicked, waiting for extension...");
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var client = GetOrCreateClient();
                var text = await Task.Run(() => client.GetElementText(BridgeDemoSelectors.BaiduSearchButton))
                    .ConfigureAwait(true);
                sw.Stop();
                AppendLog("Baidu button text: " + text + " (" + sw.ElapsedMilliseconds + " ms)");
            }
            catch (Exception ex)
            {
                AppendLog("GetText failed: " + ex.Message);
            }
            finally
            {
                btnGetBaiduButtonText.IsEnabled = true;
            }
        }

        private async void BtnClickBaiduButton_OnClick(object sender, RoutedEventArgs e)
        {
            btnClickBaiduButton.IsEnabled = false;
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var client = GetOrCreateClient();
                await Task.Run(() => client.ClickElement(BridgeDemoSelectors.BaiduSearchButton))
                    .ConfigureAwait(true);
                sw.Stop();
                AppendLog("Clicked Baidu search button via selector XML. (" + sw.ElapsedMilliseconds + " ms)");
            }
            catch (Exception ex)
            {
                AppendLog("Click failed: " + ex.Message);
            }
            finally
            {
                btnClickBaiduButton.IsEnabled = true;
            }
        }

        private async void BtnSendAlert_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var payload = BridgeCommandBuilder.Alert(txtAlertMessage.Text);
                var target = cboSendTarget.SelectedValue as string;
                await _server.SendAsync(payload, target);
                AppendLog(string.IsNullOrWhiteSpace(target)
                    ? "Sent alert command to all instances."
                    : "Sent alert command to " + target + ".");
            }
            catch (Exception ex)
            {
                AppendLog("Send failed: " + ex.Message);
            }
        }

        private void AppendLogUiOnDispatcher(string message)
        {
            if (Dispatcher.CheckAccess())
            {
                AppendLogUi(message);
                return;
            }

            Dispatcher.BeginInvoke(new Action(() => AppendLogUi(message)));
        }

        private void AppendLog(string message)
        {
            BridgeFileLog.Write(message);
            AppendLogUi(message);
        }

        private void PostLog(string message)
        {
            BridgeFileLog.Write(message);
            if (Dispatcher.CheckAccess())
            {
                AppendLogUi(message);
                return;
            }

            Dispatcher.BeginInvoke(new Action(() => AppendLogUi(message)));
        }

        private void AppendLogUi(string message)
        {
            lblLog.Text = DateTime.Now.ToString("HH:mm:ss") + "  " + message + Environment.NewLine + lblLog.Text;
        }
    }
}
