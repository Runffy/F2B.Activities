using System;
using System.Net.Http;
using System.Threading;
using F2B.Browser.Chromium.Cdp.Exceptions;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal static class CdpConnectionChecker
    {
        private const string LocalHost = "127.0.0.1";
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);

        public static bool CanConnect(int port)
        {
            return CanConnect(LocalHost, port);
        }

        public static bool CanConnect(string host, int port)
        {
            try
            {
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) })
                {
                    var response = client.GetAsync(
                        string.Format("http://{0}:{1}/json/version", host, port)).GetAwaiter().GetResult();

                    return response.IsSuccessStatusCode;
                }
            }
            catch
            {
                return false;
            }
        }

        public static void WaitUntilReady(int port, TimeSpan? timeout = null)
        {
            WaitUntilReady(LocalHost, port, timeout);
        }

        public static void WaitUntilReady(string host, int port, TimeSpan? timeout = null)
        {
            var deadline = DateTime.UtcNow + (timeout ?? DefaultTimeout);

            while (DateTime.UtcNow < deadline)
            {
                if (CanConnect(host, port))
                {
                    return;
                }

                Thread.Sleep(PollInterval);
            }

            throw new BrowserException(
                string.Format("Timed out waiting for CDP endpoint at http://{0}:{1}/json/version.", host, port));
        }
    }
}
