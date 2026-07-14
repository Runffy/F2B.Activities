using System;
using System.Collections;
using System.Collections.Generic;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal static class CdpValueConverter
    {
        public static string GetString(Dictionary<string, object> dict, string key)
        {
            if (dict == null)
            {
                return null;
            }

            object value;
            return dict.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : null;
        }

        public static int GetInt(Dictionary<string, object> dict, string key, int defaultValue = 0)
        {
            if (dict == null)
            {
                return defaultValue;
            }

            object value;
            if (!dict.TryGetValue(key, out value) || value == null)
            {
                return defaultValue;
            }

            return Convert.ToInt32(value);
        }

        public static bool GetBool(Dictionary<string, object> dict, string key)
        {
            if (dict == null)
            {
                return false;
            }

            object value;
            return dict.TryGetValue(key, out value) && value != null && Convert.ToBoolean(value);
        }

        public static Dictionary<string, object> GetDictionary(Dictionary<string, object> dict, string key)
        {
            if (dict == null)
            {
                return null;
            }

            object value;
            return dict.TryGetValue(key, out value) ? value as Dictionary<string, object> : null;
        }

        public static IList GetList(Dictionary<string, object> dict, string key)
        {
            if (dict == null)
            {
                return null;
            }

            object value;
            return dict.TryGetValue(key, out value) ? value as IList : null;
        }

        public static Dictionary<string, string> ToStringDictionary(object value)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (value == null)
            {
                return result;
            }

            var dict = value as Dictionary<string, object>;
            if (dict != null)
            {
                foreach (var pair in dict)
                {
                    result[pair.Key] = pair.Value == null ? string.Empty : Convert.ToString(pair.Value);
                }

                return result;
            }

            return result;
        }
    }
}
