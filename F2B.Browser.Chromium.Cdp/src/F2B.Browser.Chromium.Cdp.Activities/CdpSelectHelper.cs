using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    public sealed class CdpSelectedOption
    {
        public CdpSelectedOption(int index, string text, string value)
        {
            Index = index;
            Text = text ?? string.Empty;
            Value = value ?? string.Empty;
        }

        public int Index { get; private set; }

        public string Text { get; private set; }

        public string Value { get; private set; }
    }

    internal static class CdpSelectHelper
    {
        public static void Select(CdpElement element, CdpActivitySelectBy by, object[] values)
        {
            if (values == null || values.Length == 0)
            {
                return;
            }

            switch (by)
            {
                case CdpActivitySelectBy.Text:
                    element.Select(CdpSelectBy.Text, values);
                    return;
                case CdpActivitySelectBy.Value:
                    element.Select(CdpSelectBy.Value, values);
                    return;
                case CdpActivitySelectBy.Index:
                    element.Select(CdpSelectBy.Index, values);
                    return;
                case CdpActivitySelectBy.TextRegex:
                    element.Select(CdpSelectBy.Text, ResolveByRegex(element, values, true));
                    return;
                case CdpActivitySelectBy.ValueRegex:
                    element.Select(CdpSelectBy.Value, ResolveByRegex(element, values, false));
                    return;
            }
        }

        public static void Unselect(CdpElement element, CdpActivitySelectBy by, object[] values)
        {
            if (values == null || values.Length == 0)
            {
                return;
            }

            switch (by)
            {
                case CdpActivitySelectBy.Text:
                    element.Unselect(CdpSelectBy.Text, values);
                    return;
                case CdpActivitySelectBy.Value:
                    element.Unselect(CdpSelectBy.Value, values);
                    return;
                case CdpActivitySelectBy.Index:
                    element.Unselect(CdpSelectBy.Index, values);
                    return;
                case CdpActivitySelectBy.TextRegex:
                    element.Unselect(CdpSelectBy.Text, ResolveByRegex(element, values, true));
                    return;
                case CdpActivitySelectBy.ValueRegex:
                    element.Unselect(CdpSelectBy.Value, ResolveByRegex(element, values, false));
                    return;
            }
        }

        public static CdpSelectedOption[] GetSelectedOptions(CdpElement element)
        {
            var options = element.SelectedOptions;
            var result = new List<CdpSelectedOption>();
            var allOptions = element.SelectOptions;

            for (var i = 0; i < options.Length; i++)
            {
                var option = options[i];
                var index = Array.IndexOf(allOptions, option);
                result.Add(new CdpSelectedOption(
                    index >= 0 ? index : i,
                    option.Text,
                    option.Value));
            }

            return result.ToArray();
        }

        private static object[] ResolveByRegex(CdpElement element, object[] patterns, bool byText)
        {
            var matches = new List<object>();
            var options = element.SelectOptions;

            foreach (var patternObj in patterns)
            {
                var pattern = Convert.ToString(patternObj);
                if (string.IsNullOrWhiteSpace(pattern))
                {
                    continue;
                }

                var regex = new Regex(pattern);
                for (var i = 0; i < options.Length; i++)
                {
                    var candidate = byText ? options[i].Text : options[i].Value;
                    if (regex.IsMatch(candidate ?? string.Empty))
                    {
                        matches.Add(byText ? (object)candidate : options[i].Value ?? string.Empty);
                    }
                }
            }

            return matches.ToArray();
        }
    }

    internal static class CdpKeysParser
    {
        public static CdpKey[] Parse(object[] keys)
        {
            if (keys == null || keys.Length == 0)
            {
                return new CdpKey[0];
            }

            var result = new List<CdpKey>();
            foreach (var key in keys)
            {
                if (key == null)
                {
                    continue;
                }

                if (key is CdpKey cdpKey)
                {
                    result.Add(cdpKey);
                    continue;
                }

                var text = Convert.ToString(key);
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                result.Add(CdpKey.Custom(text));
            }

            return result.ToArray();
        }
    }
}
