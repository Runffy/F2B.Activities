using System;

namespace F2B.Browser.Chromium.Bridge
{
    public static class BridgeUrlRules
    {
        public static bool IsInjectableUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                   url.StartsWith("file://", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsRestrictedUrlError(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;

            return message.IndexOf("chrome://", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("Cannot access contents", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("restricted URL", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("RESTRICTED_URL:", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
