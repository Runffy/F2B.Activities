using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    /// <summary>
    /// application/x-www-form-urlencoded encoding compatible with Python urllib.parse.urlencode.
    /// </summary>
    internal static class FormUrlEncoder
    {
        public static string Encode(IDictionary<string, object> values)
        {
            if (values == null || values.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            var first = true;
            foreach (var pair in values)
            {
                if (string.IsNullOrEmpty(pair.Key))
                {
                    continue;
                }

                if (!first)
                {
                    builder.Append('&');
                }

                first = false;
                builder.Append(UrlEncode(pair.Key));
                builder.Append('=');
                builder.Append(UrlEncode(FormatValue(pair.Value)));
            }

            return builder.ToString();
        }

        private static string FormatValue(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static string UrlEncode(string value)
        {
            return Uri.EscapeDataString(value ?? string.Empty).Replace("%20", "+");
        }
    }
}
