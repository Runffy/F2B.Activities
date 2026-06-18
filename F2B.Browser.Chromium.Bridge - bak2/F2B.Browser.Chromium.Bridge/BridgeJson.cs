using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace F2B.Browser.Chromium.Bridge
{
    public sealed class BridgeRpcResponse
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();
    }

    internal static class BridgeJson
    {
        private static readonly JavaScriptSerializer Serializer = CreateSerializer();

        public static string Serialize(object value)
        {
            return Serializer.Serialize(value);
        }

        public static Dictionary<string, object> ParseObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new Dictionary<string, object>();

            var parsed = Serializer.DeserializeObject(json) as Dictionary<string, object>;
            return parsed ?? new Dictionary<string, object>();
        }

        public static string GetString(Dictionary<string, object> data, string key)
        {
            if (data == null || !data.TryGetValue(key, out var value) || value == null)
                return null;

            return Convert.ToString(value);
        }

        public static bool GetBool(Dictionary<string, object> data, string key, bool defaultValue = false)
        {
            if (data == null || !data.TryGetValue(key, out var value) || value == null)
                return defaultValue;

            if (value is bool boolValue)
                return boolValue;

            return bool.TryParse(Convert.ToString(value), out var parsed) && parsed;
        }

        public static int GetInt(Dictionary<string, object> data, string key, int defaultValue = 0)
        {
            if (data == null || !data.TryGetValue(key, out var value) || value == null)
                return defaultValue;

            return int.TryParse(Convert.ToString(value), out var parsed) ? parsed : defaultValue;
        }

        public static double GetDouble(Dictionary<string, object> data, string key, double defaultValue = 0)
        {
            if (data == null || !data.TryGetValue(key, out var value) || value == null)
                return defaultValue;

            return double.TryParse(Convert.ToString(value), out var parsed) ? parsed : defaultValue;
        }

        public static Dictionary<string, object> GetObject(Dictionary<string, object> data, string key)
        {
            if (data == null || !data.TryGetValue(key, out var value) || value == null)
                return new Dictionary<string, object>();

            return value as Dictionary<string, object> ?? new Dictionary<string, object>();
        }

        public static object[] GetArray(Dictionary<string, object> data, string key)
        {
            if (data == null || !data.TryGetValue(key, out var value) || value == null)
                return new object[0];

            return value as object[] ?? new object[0];
        }

        private static JavaScriptSerializer CreateSerializer()
        {
            return new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 64 };
        }
    }
}
