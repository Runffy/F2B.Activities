using System;
using System.Collections;
using System.Collections.Generic;
using F2B.Browser.IExplore;

namespace F2B.Browser.IExplore.Com
{
    internal enum LocatorOperation
    {
        Element,
        Input,
        Click,
        Select
    }

    internal sealed class SelectCriterion
    {
        public string Text;
        public string Value;
        public int? Index;
    }

    internal sealed class ParsedElementLocator
    {
        public Dictionary<string, string> Filters { get; set; }
        public int ElementIdx { get; set; }
        public string InputValue { get; set; }
        public MouseButton Button { get; set; }
        public ClickMode Mode { get; set; }
        public int ClickIntervalMs { get; set; } = 100;
        public IList<SelectCriterion> SelectCriteria { get; set; }
    }

    internal static class ElementLocatorParse
    {
        private static readonly HashSet<string> FilterKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ElementLocatorKeys.Id,
            ElementLocatorKeys.Tag,
            ElementLocatorKeys.Class,
            ElementLocatorKeys.Name
        };

        private static readonly HashSet<string> OperationMetadataKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ElementLocatorKeys.InputText,
            ElementLocatorKeys.Idx,
            ElementLocatorKeys.ClickButton,
            ElementLocatorKeys.ClickMode,
            ElementLocatorKeys.ClickInterval,
            ElementLocatorKeys.SelectOptionText,
            ElementLocatorKeys.SelectOptionValue,
            ElementLocatorKeys.SelectOptionIndex
        };

        /// <summary>Removes namespaced operation keys so find/exists only use element filters.</summary>
        public static void StripOperationMetadata(IDictionary<string, object> locator)
        {
            if (locator == null || locator.Count == 0)
                return;

            var remove = new List<string>();
            foreach (var key in locator.Keys)
            {
                if (OperationMetadataKeys.Contains(key))
                    remove.Add(key);
            }

            foreach (var key in remove)
                locator.Remove(key);
        }

        public static ParsedElementLocator Parse(IDictionary<string, object> locator, LocatorOperation operation)
        {
            if (locator == null || locator.Count == 0)
                throw new ArgumentException("Element locator dictionary is empty.", nameof(locator));

            var parsed = new ParsedElementLocator
            {
                Filters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                Button = MouseButton.Left,
                Mode = ClickMode.Synthetic,
                SelectCriteria = new List<SelectCriterion>()
            };

            object optionText = null;
            object optionValue = null;
            object optionIndex = null;

            foreach (var kv in locator)
            {
                if (string.IsNullOrWhiteSpace(kv.Key))
                    continue;

                var key = kv.Key.Trim();

                if (key.Equals(ElementLocatorKeys.Idx, StringComparison.OrdinalIgnoreCase))
                {
                    parsed.ElementIdx = ParseInt(kv.Value, "idx");
                    continue;
                }

                if (operation == LocatorOperation.Input
                    && key.Equals(ElementLocatorKeys.InputText, StringComparison.OrdinalIgnoreCase))
                {
                    parsed.InputValue = ToString(kv.Value);
                    continue;
                }

                if (TryParseClickKey(key, kv.Value, operation, parsed))
                    continue;

                if (TryParseSelectKey(key, kv.Value, operation, ref optionText, ref optionValue, ref optionIndex))
                    continue;

                if (FilterKeys.Contains(key))
                    parsed.Filters[key] = ToString(kv.Value);
                else
                    parsed.Filters[key] = ToString(kv.Value);
            }

            if (parsed.Filters.Count == 0)
                throw new ArgumentException("Locator must include at least one element filter (id, tag, class, name, or attribute).", nameof(locator));

            if (operation == LocatorOperation.Input && string.IsNullOrEmpty(parsed.InputValue))
            {
                throw new ArgumentException(
                    "Input locator must include \"" + ElementLocatorKeys.InputText
                    + "\", or set the Input activity Value property.",
                    nameof(locator));
            }

            if (operation == LocatorOperation.Select)
                parsed.SelectCriteria = BuildSelectCriteria(optionText, optionValue, optionIndex);

            return parsed;
        }

        private static IList<SelectCriterion> BuildSelectCriteria(object text, object value, object index)
        {
            var texts = ExpandStrings(text);
            var values = ExpandStrings(value);
            var indices = ExpandIndices(index);

            if (texts.Count == 0 && values.Count == 0 && indices.Count == 0)
            {
                throw new ArgumentException(
                    "Select locator must include one of: "
                    + ElementLocatorKeys.SelectOptionText + ", "
                    + ElementLocatorKeys.SelectOptionValue + ", "
                    + ElementLocatorKeys.SelectOptionIndex
                    + " (comma-separated or array for multiple options).",
                    "locator");
            }

            var criteria = new List<SelectCriterion>();

            if (texts.Count > 0)
            {
                foreach (var t in texts)
                    criteria.Add(new SelectCriterion { Text = t });
                return criteria;
            }

            if (values.Count > 0)
            {
                foreach (var v in values)
                    criteria.Add(new SelectCriterion { Value = v });
                return criteria;
            }

            foreach (var i in indices)
                criteria.Add(new SelectCriterion { Index = i });

            return criteria;
        }

        private static List<string> ExpandStrings(object raw)
        {
            var list = new List<string>();
            if (raw == null)
                return list;

            if (raw is string s)
            {
                foreach (var part in s.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var t = part.Trim();
                    if (t.Length > 0)
                        list.Add(t);
                }

                return list;
            }

            if (raw is IEnumerable enumerable && !(raw is string))
            {
                foreach (var item in enumerable)
                {
                    var t = ToString(item);
                    if (!string.IsNullOrEmpty(t))
                        list.Add(t);
                }

                return list;
            }

            var single = ToString(raw);
            if (!string.IsNullOrEmpty(single))
                list.Add(single);

            return list;
        }

        private static List<int> ExpandIndices(object raw)
        {
            var list = new List<int>();
            if (raw == null)
                return list;

            if (raw is int i)
            {
                list.Add(i);
                return list;
            }

            if (raw is long l)
            {
                list.Add((int)l);
                return list;
            }

            if (raw is string s)
            {
                foreach (var part in s.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    list.Add(ParseInt(part.Trim(), "index"));
                return list;
            }

            if (raw is IEnumerable enumerable && !(raw is string))
            {
                foreach (var item in enumerable)
                    list.Add(ParseInt(item, "index"));
                return list;
            }

            list.Add(ParseInt(raw, "index"));
            return list;
        }

        private static int ParseInt(object raw, string name)
        {
            if (raw is int i)
                return i;
            if (raw is long l)
                return (int)l;
            if (int.TryParse(raw?.ToString(), out var parsed))
                return parsed;
            throw new ArgumentException("Invalid " + name + " value: " + raw);
        }

        private static MouseButton ParseButton(object raw)
        {
            var s = (raw?.ToString() ?? "left").Trim();
            if (s.Equals("left", StringComparison.OrdinalIgnoreCase)) return MouseButton.Left;
            if (s.Equals("middle", StringComparison.OrdinalIgnoreCase)) return MouseButton.Middle;
            if (s.Equals("right", StringComparison.OrdinalIgnoreCase)) return MouseButton.Right;
            throw new ArgumentException("Unknown button: " + s + ". Use left, middle, or right.");
        }

        private static ClickMode ParseClickMode(object raw)
        {
            var s = (raw?.ToString() ?? "synthetic").Trim();
            if (s.Equals("synthetic", StringComparison.OrdinalIgnoreCase)
                || s.Equals("dom", StringComparison.OrdinalIgnoreCase)
                || s.Equals("virtual", StringComparison.OrdinalIgnoreCase))
                return ClickMode.Synthetic;
            if (s.Equals("physical", StringComparison.OrdinalIgnoreCase)
                || s.Equals("real", StringComparison.OrdinalIgnoreCase)
                || s.Equals("mouse", StringComparison.OrdinalIgnoreCase))
                return ClickMode.Physical;
            throw new ArgumentException("Unknown click mode: " + s + ". Use synthetic or physical.");
        }

        private static string ToString(object raw) => raw == null ? string.Empty : raw.ToString();

        /// <summary>Click meta keys apply only to <see cref="LocatorOperation.Click"/>.</summary>
        private static bool TryParseClickKey(
            string key,
            object value,
            LocatorOperation operation,
            ParsedElementLocator parsed)
        {
            if (operation != LocatorOperation.Click)
                return false;

            if (key.Equals(ElementLocatorKeys.ClickButton, StringComparison.OrdinalIgnoreCase))
            {
                parsed.Button = ParseButton(value);
                return true;
            }

            if (key.Equals(ElementLocatorKeys.ClickMode, StringComparison.OrdinalIgnoreCase))
            {
                parsed.Mode = ParseClickMode(value);
                return true;
            }

            if (key.Equals(ElementLocatorKeys.ClickInterval, StringComparison.OrdinalIgnoreCase))
            {
                parsed.ClickIntervalMs = ParseInt(value, "interval");
                return true;
            }

            return false;
        }

        /// <summary>Select option keys apply only to <see cref="LocatorOperation.Select"/> (not HTML attribute filters on radio, etc.).</summary>
        private static bool TryParseSelectKey(
            string key,
            object value,
            LocatorOperation operation,
            ref object optionText,
            ref object optionValue,
            ref object optionIndex)
        {
            if (operation != LocatorOperation.Select)
                return false;

            if (key.Equals(ElementLocatorKeys.SelectOptionText, StringComparison.OrdinalIgnoreCase))
            {
                optionText = value;
                return true;
            }

            if (key.Equals(ElementLocatorKeys.SelectOptionValue, StringComparison.OrdinalIgnoreCase))
            {
                optionValue = value;
                return true;
            }

            if (key.Equals(ElementLocatorKeys.SelectOptionIndex, StringComparison.OrdinalIgnoreCase))
            {
                optionIndex = value;
                return true;
            }

            return false;
        }
    }
}
