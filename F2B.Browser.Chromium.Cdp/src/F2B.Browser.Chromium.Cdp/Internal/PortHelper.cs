using System;
using System.Net.Sockets;
using F2B.Browser.Chromium.Cdp.Exceptions;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal static class PortHelper
    {
        private const int AutoPortMin = 9600;
        private const int AutoPortMax = 59600;
        private const string LocalHost = "127.0.0.1";

        public static bool IsPortInUse(int port)
        {
            return IsPortInUse(LocalHost, port);
        }

        public static bool IsPortInUse(string host, int port)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var result = client.BeginConnect(host, port, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(100));
                    if (!success)
                    {
                        return false;
                    }

                    client.EndConnect(result);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public static int FindAvailablePort()
        {
            var random = new Random(Guid.NewGuid().GetHashCode());
            var maxAttempts = AutoPortMax - AutoPortMin;

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                var port = random.Next(AutoPortMin, AutoPortMax + 1);
                if (!IsPortInUse(port))
                {
                    return port;
                }
            }

            throw new BrowserException(
                string.Format("Unable to find an available port in range {0}-{1}.", AutoPortMin, AutoPortMax));
        }

        public static int ResolvePort(int requestedPort)
        {
            if (requestedPort <= 0)
            {
                return FindAvailablePort();
            }

            return requestedPort;
        }
    }
}
