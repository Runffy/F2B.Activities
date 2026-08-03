using System;
using System.Threading;
using F2B.Browser.Chromium.Cdp.Browser;
using F2B.Browser.Chromium.Cdp.Exceptions;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    internal static class CdpRetryHelper
    {
        public static CdpResponse ExecuteHttpWithRetry(
            Func<CdpResponse> action,
            int timeoutMs,
            string operationName)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(0, timeoutMs));
            Exception lastError = null;

            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    return action();
                }
                catch (Exception ex) when (IsRetriable(ex))
                {
                    lastError = ex;
                    Thread.Sleep(200);
                }
            }

            if (lastError != null)
            {
                throw new BrowserException(
                    string.Format("{0} failed within {1} ms.", operationName, timeoutMs),
                    lastError);
            }

            throw new BrowserException(string.Format("{0} timed out after {1} ms.", operationName, timeoutMs));
        }

        private static bool IsRetriable(Exception ex)
        {
            return ex is BrowserException || ex is InvalidOperationException || ex is TimeoutException;
        }
    }

    internal static class CdpHttpRequestHelper
    {
        public static CdpResponse Get(CdpTab tab, string url, int timeoutMs, string[] certificationPaths)
        {
            ApplyCertificationsPlaceholder(certificationPaths);
            return CdpRetryHelper.ExecuteHttpWithRetry(() => tab.Get(url), timeoutMs, "Tab GET request");
        }

        public static CdpResponse Post(
            CdpTab tab,
            string url,
            string data,
            System.Collections.Generic.Dictionary<string, object> dict,
            int timeoutMs,
            string[] certificationPaths)
        {
            ApplyCertificationsPlaceholder(certificationPaths);
            return CdpRetryHelper.ExecuteHttpWithRetry(
                () => tab.Post(url, data, dict),
                timeoutMs,
                "Tab POST request");
        }

        private static void ApplyCertificationsPlaceholder(string[] certificationPaths)
        {
            if (certificationPaths == null || certificationPaths.Length == 0)
            {
                return;
            }

            // Client certificate injection via CDP is browser-profile specific; paths are reserved for future support.
        }
    }
}
