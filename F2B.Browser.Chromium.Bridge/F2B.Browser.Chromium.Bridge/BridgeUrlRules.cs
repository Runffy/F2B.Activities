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

        public static bool IsBlankUrl(string url)
        {
            return string.IsNullOrWhiteSpace(url) ||
                   string.Equals(url.Trim(), "about:blank", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsErrorPageUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            var lower = url.ToLowerInvariant();
            return lower.StartsWith("chrome-error://", StringComparison.Ordinal) ||
                   lower.StartsWith("edge-error://", StringComparison.Ordinal) ||
                   lower.IndexOf("chromewebdata", StringComparison.Ordinal) >= 0;
        }

        public static bool IsRestrictedUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url) || IsBlankUrl(url))
                return false;

            return !IsInjectableUrl(url);
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
