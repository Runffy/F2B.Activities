using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    /// <summary>
    /// JSON helper compatible with the Dictionary / object[] shapes formerly produced by JavaScriptSerializer.
    /// </summary>
    internal sealed class CdpJsonSerializer
    {
        private static readonly JsonSerializerSettings SerializeSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Include
        };

        public string Serialize(object value)
        {
            return JsonConvert.SerializeObject(value, SerializeSettings);
        }

        public object DeserializeObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return ToPlainClr(JToken.Parse(json));
        }

        public T Deserialize<T>(string json)
        {
            var plain = DeserializeObject(json);
            if (plain == null)
            {
                return default(T);
            }

            if (plain is T typed)
            {
                return typed;
            }

            return JsonConvert.DeserializeObject<T>(json);
        }

        private static object ToPlainClr(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
            {
                return null;
            }

            var obj = token as JObject;
            if (obj != null)
            {
                var dict = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (var property in obj.Properties())
                {
                    dict[property.Name] = ToPlainClr(property.Value);
                }

                return dict;
            }

            var array = token as JArray;
            if (array != null)
            {
                var items = new object[array.Count];
                for (var i = 0; i < array.Count; i++)
                {
                    items[i] = ToPlainClr(array[i]);
                }

                return items;
            }

            var value = token as JValue;
            if (value != null)
            {
                return value.Value;
            }

            return null;
        }
    }
}
