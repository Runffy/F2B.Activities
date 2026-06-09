using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace F2B.DesktopApplication.FlaUI.Selectors
{
    public static class PropertyMatcher
    {
        private static readonly Dictionary<string, Regex> RegexCache = new Dictionary<string, Regex>(StringComparer.Ordinal);
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(200);

        public static bool IsExactMatch(string actual, string expected, bool ignoreCase = false)
        {
            actual = actual ?? string.Empty;
            expected = expected ?? string.Empty;
            return string.Equals(actual, expected, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }

        public static bool IsRegexMatch(string actual, string pattern)
        {
            actual = actual ?? string.Empty;
            if (string.IsNullOrEmpty(pattern))
                return string.IsNullOrEmpty(actual);

            try
            {
                return GetOrCreateRegex(pattern).IsMatch(actual);
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static Regex GetOrCreateRegex(string pattern)
        {
            lock (RegexCache)
            {
                if (!RegexCache.TryGetValue(pattern, out var regex))
                {
                    regex = new Regex(pattern, RegexOptions.CultureInvariant, RegexTimeout);
                    RegexCache[pattern] = regex;
                }

                return regex;
            }
        }
    }
}
