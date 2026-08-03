using System;
using System.IO;
using System.Net;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal static class CdpTargetCloser
    {
        public static bool TryCloseTarget(int port, string targetId)
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return false;
            }

            try
            {
                var request = WebRequest.Create(
                    string.Format("http://127.0.0.1:{0}/json/close/{1}", port, targetId));
                request.Method = "GET";
                request.Timeout = 5000;

                using (var response = request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    var body = reader.ReadToEnd();
                    return body.IndexOf("Target is closing", StringComparison.OrdinalIgnoreCase) >= 0
                        || body.IndexOf("true", StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
