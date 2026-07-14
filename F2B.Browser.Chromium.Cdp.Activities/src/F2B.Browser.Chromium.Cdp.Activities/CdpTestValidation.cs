using System;
using System.IO;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    /// <summary>
    /// Shared validation helpers for OpenRPA integration test workflows.
    /// </summary>
    public static class CdpTestValidation
    {
        public static bool HttpOk(CdpResponse response, string expectedText)
        {
            return response != null
                && response.StatusCode == 200
                && string.Equals(response.Text ?? string.Empty, expectedText, StringComparison.Ordinal);
        }

        public static bool JsEqualsInt(object jsResult, int expected)
        {
            if (jsResult == null)
            {
                return false;
            }

            try
            {
                return Convert.ToInt32(jsResult) == expected;
            }
            catch
            {
                return false;
            }
        }

        public static bool FileExistsNonEmpty(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            return new FileInfo(path).Length > 0;
        }

        public static bool TabUrlContains(CdpTab tab, string fragment)
        {
            return tab != null
                && !string.IsNullOrWhiteSpace(tab.Url)
                && tab.Url.IndexOf(fragment ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool ElementTextEquals(CdpElement element, string expected)
        {
            return element != null
                && string.Equals((element.Text ?? string.Empty).Trim(), expected ?? string.Empty, StringComparison.Ordinal);
        }

        public static bool ElementAttrEquals(CdpElement element, string name, string expected)
        {
            if (element == null)
            {
                return false;
            }

            var value = element.Attr(name);
            if (expected == null)
            {
                return value == null;
            }

            return string.Equals(value ?? string.Empty, expected, StringComparison.Ordinal);
        }

        public static bool FileContentContains(string path, string fragment)
        {
            if (!FileExistsNonEmpty(path))
            {
                return false;
            }

            return File.ReadAllText(path)
                .IndexOf(fragment ?? string.Empty, StringComparison.Ordinal) >= 0;
        }

        public static bool StyleColorLooksRed(string styleValue)
        {
            if (string.IsNullOrWhiteSpace(styleValue))
            {
                return false;
            }

            return styleValue.IndexOf("255", StringComparison.Ordinal) >= 0
                || styleValue.IndexOf("red", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool StyleColorLooksBlue(string styleValue)
        {
            if (string.IsNullOrWhiteSpace(styleValue))
            {
                return false;
            }

            return styleValue.IndexOf("blue", StringComparison.OrdinalIgnoreCase) >= 0
                || styleValue.IndexOf("0, 0, 255", StringComparison.Ordinal) >= 0;
        }

        public static string JsToString(object jsResult)
        {
            if (jsResult == null)
            {
                return string.Empty;
            }

            return Convert.ToString(jsResult) ?? string.Empty;
        }
    }
}
