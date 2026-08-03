using System;
using System.Linq;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal static class FirstRunDismisser
    {
        private static readonly TimeSpan DismissTimeout = TimeSpan.FromSeconds(5);

        public static void TryDismiss(int port, string browserName)
        {
            var newTabUrl = GetNewTabUrl(browserName);

            foreach (var target in CdpTargetLister.ListPageTargets(port))
            {
                if (!IsFirstRunPage(target.Url, target.Title, browserName))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(target.WebSocketDebuggerUrl))
                {
                    continue;
                }

                try
                {
                    CdpSession.ClickStaySignedOut(target.WebSocketDebuggerUrl, DismissTimeout);
                }
                catch
                {
                    // Continue with other dismiss strategies.
                }

                try
                {
                    CdpSession.Navigate(target.WebSocketDebuggerUrl, newTabUrl, DismissTimeout);
                }
                catch
                {
                    // Continue with close attempt.
                }

                CdpTargetCloser.TryCloseTarget(port, target.Id);
            }
        }

        public static bool HasFirstRunPage(int port, string browserName)
        {
            return CdpTargetLister.ListPageTargets(port)
                .Any(target => IsFirstRunPage(target.Url, target.Title, browserName));
        }

        private static string GetNewTabUrl(string browserName)
        {
            if (string.Equals(browserName, "msedge", StringComparison.OrdinalIgnoreCase)
                || string.Equals(browserName, "edge", StringComparison.OrdinalIgnoreCase))
            {
                return "edge://newtab/";
            }

            return "chrome://newtab/";
        }

        private static bool IsFirstRunPage(string url, string title, string browserName)
        {
            if (!string.IsNullOrWhiteSpace(url))
            {
                if (url.StartsWith("chrome://intro", StringComparison.OrdinalIgnoreCase)
                    || url.StartsWith("chrome://welcome", StringComparison.OrdinalIgnoreCase)
                    || url.StartsWith("chrome://signin", StringComparison.OrdinalIgnoreCase)
                    || url.StartsWith("edge://intro", StringComparison.OrdinalIgnoreCase)
                    || url.StartsWith("edge://welcome", StringComparison.OrdinalIgnoreCase)
                    || url.StartsWith("edge://signin", StringComparison.OrdinalIgnoreCase)
                    || url.StartsWith("edge://sync-confirmation", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (IsEdge(browserName)
                    && url.IndexOf("microsoftedgewelcome.microsoft.com", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                return false;
            }

            return title.IndexOf("登录 Chrome", StringComparison.OrdinalIgnoreCase) >= 0
                || title.IndexOf("Sign in to Chrome", StringComparison.OrdinalIgnoreCase) >= 0
                || title.IndexOf("Welcome to Chrome", StringComparison.OrdinalIgnoreCase) >= 0
                || title.IndexOf("登录 Microsoft Edge", StringComparison.OrdinalIgnoreCase) >= 0
                || title.IndexOf("Sign in to Microsoft Edge", StringComparison.OrdinalIgnoreCase) >= 0
                || title.IndexOf("Welcome to Microsoft Edge", StringComparison.OrdinalIgnoreCase) >= 0
                || title.IndexOf("欢迎使用 Microsoft Edge", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsEdge(string browserName)
        {
            return string.Equals(browserName, "msedge", StringComparison.OrdinalIgnoreCase)
                || string.Equals(browserName, "edge", StringComparison.OrdinalIgnoreCase);
        }
    }
}
