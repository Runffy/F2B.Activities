using System;

namespace F2B.Browser.Chromium.Bridge
{
    internal static class BridgeUrlMatcher
    {
        public static bool Matches(string actual, string expected)
        {
            if (string.IsNullOrWhiteSpace(actual) || string.IsNullOrWhiteSpace(expected))
                return false;

            var left = Normalize(actual);
            var right = Normalize(expected);
            if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
                return true;

            if (left.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
                && right.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var leftPath = NormalizeFilePath(new Uri(left).LocalPath);
                    var rightPath = NormalizeFilePath(new Uri(right).LocalPath);
                    return string.Equals(leftPath, rightPath, StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private static string Normalize(string url)
        {
            return Uri.UnescapeDataString(url).Trim().TrimEnd('/');
        }

        private static string NormalizeFilePath(string path)
        {
            return path.Replace('/', '\\').TrimEnd('\\');
        }
    }
}
