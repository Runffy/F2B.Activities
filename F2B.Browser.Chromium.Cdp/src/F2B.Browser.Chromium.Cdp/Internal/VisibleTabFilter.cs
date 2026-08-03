using System;
using System.Collections.Generic;
using System.Linq;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal static class VisibleTabFilter
    {
        public static IList<CdpTargetInfo> ListVisibleTabs(int port)
        {
            return CdpJsonClient.ListTargets(port)
                .Where(IsVisibleTab)
                .ToList();
        }

        public static bool IsVisibleTab(CdpTargetInfo target)
        {
            if (target == null)
            {
                return false;
            }

            if (!string.Equals(target.Type, "page", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(target.Url))
            {
                return false;
            }

            var url = target.Url.Trim();
            if (url.StartsWith("devtools://", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (url.StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("extension://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("chrome-untrusted://", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (IsHiddenInternalPage(url))
            {
                return false;
            }

            return true;
        }

        private static bool IsHiddenInternalPage(string url)
        {
            return url.StartsWith("chrome://omnibox", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("edge://omnibox", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("chrome://sync-confirmation", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("edge://sync-confirmation", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("chrome://welcome-new-device", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("edge://welcome-new-device", StringComparison.OrdinalIgnoreCase);
        }
    }
}
