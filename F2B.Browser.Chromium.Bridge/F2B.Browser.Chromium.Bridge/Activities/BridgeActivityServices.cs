using System;
using System.Linq;
using System.Threading;

namespace F2B.Browser.Chromium.Bridge
{
    /// <summary>
    /// Shared Bridge host / WebSocket server for OpenRPA workflow activities.
    /// </summary>
    public static class BridgeActivityServices
    {
        private static readonly object Sync = new object();
        private static BridgeSharedSession _session;

        public static BridgeHost Host
        {
            get
            {
                EnsureStarted();
                return _session.Host;
            }
        }

        public static BridgeSessionMode Mode
        {
            get
            {
                EnsureStarted();
                return _session.Mode;
            }
        }

        public static void EnsureStarted(int port = BridgeConstants.DefaultPort)
        {
            lock (Sync)
            {
                if (_session != null && _session.IsHealthy)
                    return;

                ResetSessionInternal();
                _session = BridgeSharedSession.Connect(port, "OpenRPA Bridge");
            }
        }

        /// <summary>
        /// Drops the cached Bridge session so the next call reconnects (e.g. after Inspector was closed).
        /// </summary>
        public static void ResetSession()
        {
            lock (Sync)
            {
                ResetSessionInternal();
            }
        }

        private static void ResetSessionInternal()
        {
            if (_session == null)
                return;

            try
            {
                _session.Dispose();
            }
            catch
            {
            }

            _session = null;
        }

        public static bool IsExtensionConnected(string preferredInstanceId = null)
        {
            EnsureStarted();

            var clients = _session.GetConnectedClients();
            if (clients.Count == 0)
                return false;

            if (!string.IsNullOrWhiteSpace(preferredInstanceId))
            {
                return clients.Any(item =>
                    string.Equals(item.InstanceId, preferredInstanceId, StringComparison.OrdinalIgnoreCase));
            }

            return true;
        }

        public static bool TryWaitForExtension(
            TimeSpan timeout,
            out string instanceId,
            string preferredInstanceId = null)
        {
            EnsureStarted();

            instanceId = null;
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                var clients = _session.GetConnectedClients();
                if (clients.Count > 0)
                {
                    if (!string.IsNullOrWhiteSpace(preferredInstanceId))
                    {
                        var matched = clients.FirstOrDefault(
                            item => string.Equals(item.InstanceId, preferredInstanceId, StringComparison.OrdinalIgnoreCase));
                        if (matched != null)
                        {
                            instanceId = matched.InstanceId;
                            return true;
                        }
                    }
                    else
                    {
                        instanceId = clients[0].InstanceId;
                        return true;
                    }
                }

                Thread.Sleep(250);
            }

            return false;
        }

        public static string WaitForExtension(TimeSpan timeout, string preferredInstanceId = null)
        {
            if (TryWaitForExtension(timeout, out var instanceId, preferredInstanceId))
                return instanceId;

            if (BridgeChromiumLauncher.IsChromiumProcessRunning())
            {
                throw new TimeoutException(
                    "Chromium is running but the F2B Bridge extension did not connect within "
                    + (int)timeout.TotalSeconds
                    + " seconds. Reload the extension in chrome://extensions or set Extension Path on Open Browser.");
            }

            throw new TimeoutException(
                "No Chromium extension connected to Bridge within "
                + (int)timeout.TotalSeconds
                + " seconds. Ensure Chrome is installed and the F2B extension is available.");
        }

        public static BridgeSyncClient ResolveClient(string instanceId = null, TimeSpan? connectTimeout = null)
        {
            var timeout = connectTimeout ?? TimeSpan.FromSeconds(60);
            var resolvedInstanceId = WaitForExtension(timeout, instanceId);
            return Host.GetClient(resolvedInstanceId);
        }

        public static BwBrowser GetBrowser(string instanceId = null, TimeSpan? connectTimeout = null)
        {
            return ResolveClient(instanceId, connectTimeout).GetBrowser();
        }

        public static BridgeAttachResult AttachByWndSelector(string selectorXml, TimeSpan? connectTimeout = null)
        {
            EnsureStarted();

            var timeout = connectTimeout ?? TimeSpan.FromSeconds(60);
            var deadline = DateTime.UtcNow.Add(timeout);

            if (!TryWaitForExtensionUntil(deadline, out _))
            {
                throw new TimeoutException(
                    "No Chromium extension connected to Bridge within "
                    + (int)timeout.TotalSeconds
                    + " seconds.");
            }

            var remainingMs = Math.Max(0, (int)(deadline - DateTime.UtcNow).TotalMilliseconds);
            return Host.AttachByWndSelector(selectorXml, remainingMs);
        }

        private static bool TryWaitForExtensionUntil(DateTime deadline, out string instanceId, string preferredInstanceId = null)
        {
            EnsureStarted();
            instanceId = null;

            while (DateTime.UtcNow < deadline)
            {
                var clients = _session.GetConnectedClients();
                if (clients.Count > 0)
                {
                    if (!string.IsNullOrWhiteSpace(preferredInstanceId))
                    {
                        var matched = clients.FirstOrDefault(
                            item => string.Equals(item.InstanceId, preferredInstanceId, StringComparison.OrdinalIgnoreCase));
                        if (matched != null)
                        {
                            instanceId = matched.InstanceId;
                            return true;
                        }
                    }
                    else
                    {
                        instanceId = clients[0].InstanceId;
                        return true;
                    }
                }

                Thread.Sleep(250);
            }

            return false;
        }
    }
}
