using System;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal static class BrowserNameHelper
    {
        public static string InferBrowserName(string browserField, string userAgent)
        {
            var browser = browserField ?? string.Empty;
            var agent = userAgent ?? string.Empty;
            var combined = browser + " " + agent;

            if (combined.IndexOf("Edge", StringComparison.OrdinalIgnoreCase) >= 0
                || combined.IndexOf("Edg/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "msedge";
            }

            if (combined.IndexOf("Chrome", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "chrome";
            }

            return "unknown";
        }

        public static bool IsSupportedBrowser(string browserName)
        {
            return string.Equals(browserName, "chrome", StringComparison.OrdinalIgnoreCase)
                || string.Equals(browserName, "msedge", StringComparison.OrdinalIgnoreCase);
        }

        public static bool MatchesBrowserFilter(string browserName, string browserFilter)
        {
            if (string.IsNullOrWhiteSpace(browserFilter))
            {
                return true;
            }

            var filter = browserFilter.Trim();
            if (string.Equals(filter, "edge", StringComparison.OrdinalIgnoreCase)
                || string.Equals(filter, "msedge", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(browserName, "msedge", StringComparison.OrdinalIgnoreCase);
            }

            if (string.Equals(filter, "chrome", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(browserName, "chrome", StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(browserName, filter, StringComparison.OrdinalIgnoreCase);
        }
    }
}
