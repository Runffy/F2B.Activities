using System;
using System.Collections;
using System.Collections.Generic;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal static class CdpLinkHelper
    {
        public static string MakeAbsolute(string link, string baseUri)
        {
            if (string.IsNullOrWhiteSpace(link))
            {
                return link;
            }

            link = link.Trim().Replace('\\', '/');
            if (link.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
                link.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            {
                return link;
            }

            Uri absolute;
            if (Uri.TryCreate(link, UriKind.Absolute, out absolute) && !string.IsNullOrEmpty(absolute.Host))
            {
                return link;
            }

            if (string.IsNullOrWhiteSpace(baseUri))
            {
                return link;
            }

            Uri baseAddress;
            if (!Uri.TryCreate(baseUri, UriKind.Absolute, out baseAddress))
            {
                return link;
            }

            Uri combined;
            return Uri.TryCreate(baseAddress, link, out combined) ? combined.ToString() : link;
        }
    }
}
