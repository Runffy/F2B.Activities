using System;
using F2B.Browser.Chromium.Bridge;

namespace F2B.Browser.Chromium.Inspector.Services
{
    public sealed class BridgeInspectorSession : IDisposable
    {
        private BridgeSharedSession _session;
        private BridgeSyncClient _client;
        private string _instanceId;

        public event EventHandler ClientsChanged;

        public BridgeSessionMode Mode => _session?.Mode ?? BridgeSessionMode.Owner;

        public bool IsConnected
        {
            get
            {
                try
                {
                    return GetConnectedClients().Count > 0;
                }
                catch
                {
                    return false;
                }
            }
        }

        public int ConnectedCount
        {
            get
            {
                try
                {
                    return GetConnectedClients().Count;
                }
                catch
                {
                    return 0;
                }
            }
        }

        public BridgeHost Host => _session?.Host;

        public void Start()
        {
            if (_session != null)
                return;

            _session = BridgeSharedSession.Connect(
                BridgeConstants.DefaultPort,
                "F2B Chromium Inspector");
            _session.ClientsChanged += OnClientsChanged;
        }

        public void Reconnect()
        {
            if (_session != null)
                _session.ClientsChanged -= OnClientsChanged;

            _client?.Dispose();
            _client = null;
            _instanceId = null;
            _session?.Dispose();
            _session = null;

            Start();
        }

        public BwTab RefreshTargetTab(BwTab tab)
        {
            if (tab == null)
                return GetTargetTab();

            try
            {
                var info = tab.GetInfo();
                if (info.IsClosed)
                    return GetTargetTab();

                tab.Activate();
                return tab;
            }
            catch
            {
                return GetTargetTab();
            }
        }

        public BwTab GetTargetTab()
        {
            var client = GetClient();
            var browser = client.GetBrowser();
            var tab = browser.GetActivatedTab();
            if (tab == null)
                throw new InvalidOperationException("No active tab found in the connected browser.");

            try
            {
                var url = tab.GetInfo()?.Url;
                if (!BridgeUrlRules.IsInjectableUrl(url))
                {
                    throw new InvalidOperationException(
                        "The active tab uses a restricted URL (" + (url ?? "unknown") +
                        "). Switch to a normal http/https page before using Indicate.");
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch
            {
            }

            return tab;
        }

        public BridgeSyncClient GetClient(string instanceId = null)
        {
            var clients = GetConnectedClients();
            if (clients.Count == 0)
            {
                throw new InvalidOperationException(
                    Mode == BridgeSessionMode.Attached
                        ? "Chrome extension is not connected to the shared Bridge host. Ensure Chrome extension is online."
                        : "Chrome extension is not connected. Open Chrome with the F2B Bridge extension loaded.");
            }

            var targetId = string.IsNullOrWhiteSpace(instanceId)
                ? clients[0].InstanceId
                : instanceId;

            if (_client == null || !string.Equals(_instanceId, targetId, StringComparison.OrdinalIgnoreCase))
            {
                _client?.Dispose();
                _client = _session.Host.GetClient(targetId);
                _instanceId = targetId;
            }

            return _client;
        }

        private System.Collections.Generic.IReadOnlyList<BridgeClientInfo> GetConnectedClients()
        {
            if (_session == null)
                return new BridgeClientInfo[0];

            return _session.GetConnectedClients();
        }

        private void OnClientsChanged(object sender, EventArgs e)
        {
            ClientsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (_session != null)
                _session.ClientsChanged -= OnClientsChanged;

            _client?.Dispose();
            _client = null;
            _session?.Dispose();
            _session = null;
        }
    }
}
