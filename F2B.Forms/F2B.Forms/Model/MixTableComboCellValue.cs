using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace F2B.Forms.Model
{
    /// <summary>
    /// ComboBox cell payload for Initial Rows / Bind:
    /// shorthand string, or { "value": "...", "items": ["..."] } for per-row options.
    /// </summary>
    public sealed class MixTableComboCellValue
    {
        public string Value { get; set; }

        /// <summary>
        /// Null / empty = use column default items.
        /// Non-empty = per-row ComboBox options override.
        /// </summary>
        public List<string> ItemsOverride { get; set; }

        public bool HasItemsOverride
        {
            get { return ItemsOverride != null && ItemsOverride.Count > 0; }
        }

        public static MixTableComboCellValue Parse(object raw)
        {
            if (raw == null || raw == DBNull.Value)
            {
                return new MixTableComboCellValue { Value = string.Empty };
            }

            if (raw is MixTableComboCellValue typed)
            {
                return new MixTableComboCellValue
                {
                    Value = typed.Value ?? string.Empty,
                    ItemsOverride = typed.ItemsOverride == null
                        ? null
                        : new List<string>(typed.ItemsOverride)
                };
            }

            if (raw is string || raw is System.Text.StringBuilder)
            {
                return new MixTableComboCellValue
                {
                    Value = Convert.ToString(raw) ?? string.Empty
                };
            }

            JObject jObject = raw as JObject;
            if (jObject != null)
            {
                return FromObject(
                    jObject["value"] ?? jObject["Value"],
                    jObject["items"] ?? jObject["Items"]);
            }

            IDictionary dictionary = raw as IDictionary;
            if (dictionary != null)
            {
                object valueToken = null;
                object itemsToken = null;
                foreach (DictionaryEntry entry in dictionary)
                {
                    string key = Convert.ToString(entry.Key) ?? string.Empty;
                    if (string.Equals(key, "value", StringComparison.OrdinalIgnoreCase))
                    {
                        valueToken = entry.Value;
                    }
                    else if (string.Equals(key, "items", StringComparison.OrdinalIgnoreCase))
                    {
                        itemsToken = entry.Value;
                    }
                }

                // Plain dictionary without value/items keys is not a combo payload.
                if (valueToken != null || itemsToken != null)
                {
                    return FromObject(valueToken, itemsToken);
                }
            }

            return new MixTableComboCellValue
            {
                Value = Convert.ToString(raw) ?? string.Empty
            };
        }

        /// <summary>
        /// Serialize for Initial Rows Json: string when no override; object when items overridden.
        /// </summary>
        public static object ToJsonPayload(string value, IList<string> itemsOverride)
        {
            string text = value ?? string.Empty;
            List<string> normalized = NormalizeItems(itemsOverride);
            if (normalized.Count == 0)
            {
                return text;
            }

            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "value", text },
                { "items", normalized }
            };
        }

        public static List<string> ResolveEffectiveItems(
            MixTableColumnDefinition column,
            MixTableComboCellValue cell)
        {
            if (cell != null && cell.HasItemsOverride)
            {
                return NormalizeItems(cell.ItemsOverride);
            }

            return NormalizeItems(column == null ? null : column.Items);
        }

        public static List<string> NormalizeItems(IEnumerable<string> items)
        {
            var list = new List<string>();
            if (items == null)
            {
                return list;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string item in items)
            {
                string text = item ?? string.Empty;
                if (!seen.Add(text))
                {
                    continue;
                }

                list.Add(text);
            }

            return list;
        }

        private static MixTableComboCellValue FromObject(object valueToken, object itemsToken)
        {
            var result = new MixTableComboCellValue
            {
                Value = UnwrapToString(valueToken)
            };

            if (itemsToken == null || itemsToken == DBNull.Value)
            {
                return result;
            }

            List<string> items = ReadItems(itemsToken);
            // Per plan: only non-empty items arrays count as a row-level override.
            result.ItemsOverride = items.Count == 0 ? null : items;
            return result;
        }

        private static List<string> ReadItems(object itemsToken)
        {
            var list = new List<string>();
            JArray jArray = itemsToken as JArray;
            if (jArray != null)
            {
                foreach (JToken token in jArray)
                {
                    list.Add(token == null || token.Type == JTokenType.Null
                        ? string.Empty
                        : token.ToString());
                }

                return NormalizeItems(list);
            }

            IEnumerable enumerable = itemsToken as IEnumerable;
            if (enumerable != null && !(itemsToken is string))
            {
                foreach (object item in enumerable)
                {
                    list.Add(UnwrapToString(item));
                }

                return NormalizeItems(list);
            }

            string single = UnwrapToString(itemsToken);
            if (!string.IsNullOrEmpty(single))
            {
                list.Add(single);
            }

            return list;
        }

        private static string UnwrapToString(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return string.Empty;
            }

            JValue jValue = value as JValue;
            if (jValue != null)
            {
                return jValue.Value == null ? string.Empty : Convert.ToString(jValue.Value) ?? string.Empty;
            }

            return Convert.ToString(value) ?? string.Empty;
        }
    }
}
