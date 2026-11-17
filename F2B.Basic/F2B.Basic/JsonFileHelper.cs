using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace F2B.Basic
{
    internal static class JsonFileHelper
    {
        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Include
        };

        internal static Dictionary<string, object> LoadDictionary(string filePath)
        {
            string path = RequirePath(filePath);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("JSON file was not found: " + path, path);
            }

            string text = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(text))
            {
                return new Dictionary<string, object>(StringComparer.Ordinal);
            }

            JToken root = JToken.Parse(text);
            if (root == null || root.Type == JTokenType.Null)
            {
                return new Dictionary<string, object>(StringComparer.Ordinal);
            }

            if (root.Type != JTokenType.Object)
            {
                throw new InvalidOperationException(
                    "JSON root must be an object to load as Dictionary<string, object>. Actual type: " + root.Type);
            }

            return ToDictionary((JObject)root);
        }

        internal static void SaveDictionary(string filePath, Dictionary<string, object> data)
        {
            string path = RequirePath(filePath);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var payload = data ?? new Dictionary<string, object>(StringComparer.Ordinal);
            string json = JsonConvert.SerializeObject(payload, SerializerSettings);
            File.WriteAllText(path, json);
        }

        internal static void Dump(string filePath, Dictionary<string, object> data, JsonDumpMode mode)
        {
            string path = RequirePath(filePath);
            var incoming = data ?? new Dictionary<string, object>(StringComparer.Ordinal);

            if (mode == JsonDumpMode.Overwrite || !File.Exists(path))
            {
                SaveDictionary(path, incoming);
                return;
            }

            Dictionary<string, object> existing = LoadDictionary(path);
            foreach (KeyValuePair<string, object> pair in incoming)
            {
                existing[pair.Key] = pair.Value;
            }

            SaveDictionary(path, existing);
        }

        internal static object GetKeyValue(string filePath, string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Key is required.", nameof(key));
            }

            Dictionary<string, object> dictionary = LoadDictionary(filePath);
            if (!dictionary.TryGetValue(key.Trim(), out object value))
            {
                throw new KeyNotFoundException("JSON key was not found: " + key.Trim());
            }

            return value;
        }

        internal static T ConvertJsonValueTo<T>(object raw)
        {
            Type target = typeof(T);
            Type underlying = Nullable.GetUnderlyingType(target) ?? target;

            if (raw == null || raw is DBNull || (raw is JToken jtNull && jtNull.Type == JTokenType.Null))
            {
                if (target.IsValueType && Nullable.GetUnderlyingType(target) == null)
                {
                    throw new InvalidCastException(
                        "Cannot convert null JSON value to non-nullable type '" + target.FullName + "'.");
                }

                return default(T);
            }

            if (raw is JToken token)
            {
                try
                {
                    return token.ToObject<T>();
                }
                catch (Exception ex)
                {
                    throw new InvalidCastException(
                        "Cannot convert JSON token of type '" + token.Type
                        + "' to '" + target.FullName + "'.",
                        ex);
                }
            }

            if (raw is T typed)
            {
                return typed;
            }

            try
            {
                if (underlying.IsEnum)
                {
                    if (raw is string enumText)
                    {
                        return (T)Enum.Parse(underlying, enumText, ignoreCase: true);
                    }

                    return (T)Enum.ToObject(underlying, raw);
                }

                if (underlying == typeof(Guid))
                {
                    return (T)(object)Guid.Parse(Convert.ToString(raw, CultureInfo.InvariantCulture));
                }

                object converted = Convert.ChangeType(raw, underlying, CultureInfo.InvariantCulture);
                return (T)converted;
            }
            catch (Exception ex)
            {
                throw new InvalidCastException(
                    "Cannot convert JSON value of type '" + raw.GetType().FullName
                    + "' to '" + target.FullName + "'.",
                    ex);
            }
        }

        private static Dictionary<string, object> ToDictionary(JObject obj)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, JToken> property in obj)
            {
                result[property.Key] = ToClrValue(property.Value);
            }

            return result;
        }

        private static object ToClrValue(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
            {
                return null;
            }

            switch (token.Type)
            {
                case JTokenType.Object:
                    return ToDictionary((JObject)token);
                case JTokenType.Array:
                    var list = new List<object>();
                    foreach (JToken item in (JArray)token)
                    {
                        list.Add(ToClrValue(item));
                    }

                    return list;
                case JTokenType.Integer:
                case JTokenType.Float:
                case JTokenType.String:
                case JTokenType.Boolean:
                case JTokenType.Date:
                case JTokenType.Guid:
                case JTokenType.TimeSpan:
                case JTokenType.Uri:
                case JTokenType.Bytes:
                    return ((JValue)token).Value;
                default:
                    return ((JValue)token).Value;
            }
        }

        private static string RequirePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File Path is required.", nameof(filePath));
            }

            return Path.GetFullPath(filePath.Trim());
        }
    }
}
