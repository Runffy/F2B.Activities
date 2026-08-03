using System;
using System.Text.RegularExpressions;
using F2B.Browser.Chromium.Cdp.Exceptions;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal static class CdpElementPropertyHelper
    {
        private static readonly Regex PropertyNamePattern = new Regex(
            @"^[a-zA-Z_$][a-zA-Z0-9_$]*$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        internal static string NormalizePropertyName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new BrowserException("Property name is required.");
            }

            var trimmed = name.Trim();
            if (!PropertyNamePattern.IsMatch(trimmed))
            {
                throw new BrowserException("Invalid DOM property name: " + trimmed);
            }

            return trimmed;
        }
    }
}
