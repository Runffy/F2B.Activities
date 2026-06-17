using System;
using System.Text.RegularExpressions;

namespace F2B.Browser.Chromium.Bridge
{
    internal static class BridgeMessageParser
    {
        private static readonly Regex TypeRegex = new Regex(
            "\"type\"\\s*:\\s*\"(?<value>[^\"]+)\"",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex InstanceIdRegex = new Regex(
            "\"instanceId\"\\s*:\\s*\"(?<value>[^\"]+)\"",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex LabelRegex = new Regex(
            "\"label\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"\\\\])*)\"",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex RoleRegex = new Regex(
            "\"role\"\\s*:\\s*\"(?<value>[^\"]+)\"",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static bool TryGetType(string json, out string type)
        {
            type = null;
            if (string.IsNullOrWhiteSpace(json))
                return false;

            var match = TypeRegex.Match(json);
            if (!match.Success)
                return false;

            type = match.Groups["value"].Value;
            return !string.IsNullOrEmpty(type);
        }

        public static bool TryParseHello(string json, out string instanceId, out string label)
        {
            return TryParseHello(json, out instanceId, out label, out _);
        }

        public static bool TryParseHello(string json, out string instanceId, out string label, out BridgeClientRole role)
        {
            instanceId = null;
            label = string.Empty;
            role = BridgeClientRole.Extension;

            if (!TryGetType(json, out var type) || !string.Equals(type, "hello", StringComparison.Ordinal))
                return false;

            var instanceMatch = InstanceIdRegex.Match(json);
            if (!instanceMatch.Success)
                return false;

            instanceId = UnescapeJson(instanceMatch.Groups["value"].Value);
            if (string.IsNullOrWhiteSpace(instanceId))
                return false;

            var labelMatch = LabelRegex.Match(json);
            if (labelMatch.Success)
                label = UnescapeJson(labelMatch.Groups["value"].Value);

            var roleMatch = RoleRegex.Match(json);
            if (roleMatch.Success &&
                string.Equals(roleMatch.Groups["value"].Value, "controller", StringComparison.OrdinalIgnoreCase))
            {
                role = BridgeClientRole.Controller;
            }

            return true;
        }

        public static bool TryGetInstanceId(string json, out string instanceId)
        {
            instanceId = null;
            if (string.IsNullOrWhiteSpace(json))
                return false;

            var match = InstanceIdRegex.Match(json);
            if (!match.Success)
                return false;

            instanceId = UnescapeJson(match.Groups["value"].Value);
            return !string.IsNullOrWhiteSpace(instanceId);
        }

        private static string UnescapeJson(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\")
                .Replace("\\r", "\r")
                .Replace("\\n", "\n");
        }
    }
}
