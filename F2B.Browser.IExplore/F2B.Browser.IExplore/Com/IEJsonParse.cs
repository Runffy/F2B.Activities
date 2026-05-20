using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Web.Script.Serialization;
using F2B.Browser.IExplore;

namespace F2B.Browser.IExplore.Com
{
    internal static class IEJsonParse
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public static Dictionary<string, object> ParseDictionary(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            var normalized = NormalizeToJson(json.Trim());
            var raw = Serializer.DeserializeObject(normalized);
            return ToStringObjectDictionary(raw);
        }

        public static IList<IDictionary<string, object>> ParseFramePath(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            var normalized = NormalizeToJson(json.Trim());
            var raw = Serializer.DeserializeObject(normalized);

            var list = raw as IList;
            if (list != null)
                return ParseFrameSegmentList(list);

            var dict = raw as IDictionary;
            if (dict != null && dict.Contains("frame"))
                return ParseFrameValue(dict["frame"]);

            throw new ArgumentException("Frame JSON must be an array or an object with a \"frame\" property.", nameof(json));
        }

        public static void ParseLocator(
            string json,
            out Dictionary<string, object> element,
            out IList<IDictionary<string, object>> framePath)
        {
            element = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            framePath = null;

            if (string.IsNullOrWhiteSpace(json))
                return;

            var normalized = NormalizeToJson(json.Trim());
            var raw = Serializer.DeserializeObject(normalized);
            var dict = raw as IDictionary;
            if (dict == null)
                throw new ArgumentException("Locator JSON must be a JSON object.", nameof(json));

            if (dict.Contains("element"))
                element = ToStringObjectDictionary(dict["element"]);

            if (dict.Contains("frame"))
                framePath = ParseFrameValue(dict["frame"]);
            else
            {
                foreach (DictionaryEntry entry in dict)
                {
                    var key = entry.Key?.ToString();
                    if (string.IsNullOrEmpty(key))
                        continue;
                    if (key.Equals("element", StringComparison.OrdinalIgnoreCase)
                        || key.Equals("frame", StringComparison.OrdinalIgnoreCase))
                        continue;

                    element[key] = entry.Value;
                }
            }
        }

        /// <summary>Convert VB-friendly single-quoted strings to double-quoted JSON.</summary>
        public static string NormalizeToJson(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('\'') < 0)
                return text;

            var sb = new StringBuilder(text.Length + 8);
            var inDouble = false;
            var inSingle = false;

            for (int i = 0; i < text.Length; i++)
            {
                var c = text[i];

                if (inSingle)
                {
                    if (c == '\\' && i + 1 < text.Length)
                    {
                        sb.Append(c);
                        sb.Append(text[++i]);
                        continue;
                    }

                    if (c == '\'')
                    {
                        sb.Append('"');
                        inSingle = false;
                        continue;
                    }

                    if (c == '"')
                        sb.Append('\\');
                    sb.Append(c);
                    continue;
                }

                if (inDouble)
                {
                    if (c == '\\' && i + 1 < text.Length)
                    {
                        sb.Append(c);
                        sb.Append(text[++i]);
                        continue;
                    }

                    if (c == '"')
                        inDouble = false;
                    sb.Append(c);
                    continue;
                }

                if (c == '\'')
                {
                    sb.Append('"');
                    inSingle = true;
                    continue;
                }

                if (c == '"')
                {
                    inDouble = true;
                    sb.Append(c);
                    continue;
                }

                sb.Append(c);
            }

            return sb.ToString();
        }

        private static IList<IDictionary<string, object>> ParseFrameValue(object raw)
        {
            var list = raw as IList;
            if (list == null)
                throw new ArgumentException("Frame value must be a JSON array.");

            return ParseFrameSegmentList(list);
        }

        private static IList<IDictionary<string, object>> ParseFrameSegmentList(IList list)
        {
            var path = new List<IDictionary<string, object>>(list.Count);
            foreach (var item in list)
            {
                if (item is string s)
                {
                    path.Add(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        { FrameLocatorKeys.Name, s }
                    });
                    continue;
                }

                var seg = ToStringObjectDictionary(item);
                if (seg.Count != 1)
                    throw new ArgumentException("Each frame segment must have exactly one key.");

                path.Add(seg);
            }

            return path;
        }

        private static Dictionary<string, object> ToStringObjectDictionary(object raw)
        {
            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (raw == null)
                return dict;

            var idict = raw as IDictionary;
            if (idict == null)
                throw new ArgumentException("Expected a JSON object.");

            foreach (DictionaryEntry entry in idict)
            {
                var key = entry.Key?.ToString();
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                dict[key.Trim()] = NormalizeValue(entry.Value);
            }

            return dict;
        }

        private static object NormalizeValue(object value)
        {
            if (value == null)
                return null;

            if (value is string || value is bool)
                return value;

            if (value is int || value is long || value is double || value is decimal)
                return value;

            if (value is ArrayList list)
            {
                var arr = new object[list.Count];
                for (int i = 0; i < list.Count; i++)
                    arr[i] = NormalizeValue(list[i]);
                return arr;
            }

            return value.ToString();
        }

        /// <summary>Removes a key for find-by-locator (e.g. strip Input text <c>value</c>, keep HTML attribute <c>value</c> on radio).</summary>
        public static void RemoveKeyIgnoreCase(IDictionary<string, object> dict, string key)
        {
            if (dict == null || string.IsNullOrEmpty(key))
                return;

            string found = null;
            foreach (var kv in dict)
            {
                if (key.Equals(kv.Key, StringComparison.OrdinalIgnoreCase))
                {
                    found = kv.Key;
                    break;
                }
            }

            if (found != null)
                dict.Remove(found);
        }
    }
}
