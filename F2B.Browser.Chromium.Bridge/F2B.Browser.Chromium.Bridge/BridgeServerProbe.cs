using System;
using System.IO;
using System.Net;

namespace F2B.Browser.Chromium.Bridge
{
    public static class BridgeServerProbe
    {
        public static bool IsBridgeListening(int port = BridgeConstants.DefaultPort, int timeoutMs = 2000)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(
                    "http://" + BridgeConstants.DefaultHost + ":" + port + "/health");
                request.Method = "GET";
                request.Timeout = timeoutMs;
                request.ReadWriteTimeout = timeoutMs;

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    return response.StatusCode == HttpStatusCode.OK;
                }
            }
            catch (WebException ex)
            {
                if (ex.Response is HttpWebResponse httpResponse &&
                    httpResponse.StatusCode == HttpStatusCode.OK)
                {
                    return true;
                }

                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
