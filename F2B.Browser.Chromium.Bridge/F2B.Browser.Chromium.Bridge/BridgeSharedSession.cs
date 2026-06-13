using System;
using System.Collections.Generic;
using System.Net;

namespace F2B.Browser.Chromium.Bridge
{
    /// <summary>
    /// Connects to Bridge as owner (WebSocket server) or controller client (attach to an existing host).
    /// Either OpenRPA or Inspector may own port 19222; the other process attaches automatically.
    /// </summary>
    public sealed class BridgeSharedSession : IDisposable
    {
        private BridgeWebSocketServer _server;
        private BridgeRemoteRpcHost _remoteRpc;

        private BridgeSharedSession(
            BridgeSessionMode mode,
            BridgeHost host,
            BridgeWebSocketServer server,
            BridgeRemoteRpcHost remoteRpc)
        {
            Mode = mode;
            Host = host ?? throw new ArgumentNullException(nameof(host));
            _server = server;
            _remoteRpc = remoteRpc;
        }

        public BridgeSessionMode Mode { get; }

        public BridgeHost Host { get; }

        public event EventHandler ClientsChanged;

        public static BridgeSharedSession Connect(
            int port = BridgeConstants.DefaultPort,
            string controllerLabel = "F2B Bridge Controller")
        {
            if (BridgeServerProbe.IsBridgeListening(port))
                return Attach(port, controllerLabel);

            try
            {
                return CreateOwner(port);
            }
            catch (HttpListenerException)
            {
                if (!BridgeServerProbe.IsBridgeListening(port))
                    throw;

                return Attach(port, controllerLabel);
            }
        }

        public IReadOnlyList<BridgeClientInfo> GetConnectedClients()
        {
            if (Mode == BridgeSessionMode.Attached)
                return _remoteRpc.ListExtensionClients();

            return _server.GetConnectedClients();
        }

        public void Dispose()
        {
            if (_server != null)
                _server.ClientsChanged -= OnServerClientsChanged;

            if (_remoteRpc != null)
                _remoteRpc.ClientsChanged -= OnRemoteClientsChanged;

            Host.Dispose();
            _remoteRpc?.Dispose();
            _remoteRpc = null;
            _server?.Dispose();
            _server = null;
        }

        private static BridgeSharedSession CreateOwner(int port)
        {
            var server = new BridgeWebSocketServer(port);
            server.Start();
            var host = new BridgeHost(server);
            var session = new BridgeSharedSession(BridgeSessionMode.Owner, host, server, null);
            server.ClientsChanged += session.OnServerClientsChanged;
            return session;
        }

        private static BridgeSharedSession Attach(int port, string controllerLabel)
        {
            var remoteRpc = BridgeRemoteRpcHost.Connect(port, controllerLabel);
            var host = BridgeHost.Attach(remoteRpc);
            var session = new BridgeSharedSession(BridgeSessionMode.Attached, host, null, remoteRpc);
            remoteRpc.ClientsChanged += session.OnRemoteClientsChanged;
            return session;
        }

        private void OnServerClientsChanged(object sender, EventArgs e)
        {
            ClientsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnRemoteClientsChanged(object sender, EventArgs e)
        {
            ClientsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
