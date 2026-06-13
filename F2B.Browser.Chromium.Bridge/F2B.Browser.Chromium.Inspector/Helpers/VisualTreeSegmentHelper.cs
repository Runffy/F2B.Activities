using System;
using System.Collections.Generic;

namespace F2B.Browser.Chromium.Inspector.Helpers
{
    internal static class VisualTreeSegmentHelper
    {
        public static bool SegmentsEqual(object[] left, object[] right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (left == null || right == null)
                return false;

            if (left.Length != right.Length)
                return false;

            for (var i = 0; i < left.Length; i++)
            {
                if (!SegmentEquals(left[i], right[i]))
                    return false;
            }

            return true;
        }

        public static bool SegmentEquals(object left, object right)
        {
            var leftMap = left as Dictionary<string, object>;
            var rightMap = right as Dictionary<string, object>;
            if (leftMap == null || rightMap == null)
                return Equals(left, right);

            var leftType = NormalizeType(GetString(leftMap, "type"));
            var rightType = NormalizeType(GetString(rightMap, "type"));
            if (!string.Equals(leftType, rightType, StringComparison.OrdinalIgnoreCase))
                return false;

            return GetInt(leftMap, "index") == GetInt(rightMap, "index");
        }

        private static string GetString(Dictionary<string, object> map, string key)
        {
            if (map == null || !map.TryGetValue(key, out var value) || value == null)
                return string.Empty;

            return Convert.ToString(value) ?? string.Empty;
        }

        private static int GetInt(Dictionary<string, object> map, string key)
        {
            if (map == null || !map.TryGetValue(key, out var value) || value == null)
                return 0;

            if (value is int intValue)
                return intValue;

            if (int.TryParse(Convert.ToString(value), out var parsed))
                return parsed;

            return 0;
        }

        private static string NormalizeType(string type)
        {
            if (string.IsNullOrWhiteSpace(type))
                return "dom";

            return type.Equals("enterframe", StringComparison.OrdinalIgnoreCase)
                ? "enterFrame"
                : type;
        }
    }
}
