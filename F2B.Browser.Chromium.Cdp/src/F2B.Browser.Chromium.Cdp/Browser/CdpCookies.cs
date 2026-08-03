using System;
using System.Collections.Generic;
using System.Linq;
using F2B.Browser.Chromium.Cdp.Internal;

namespace F2B.Browser.Chromium.Cdp.Browser
{
    /// <summary>
    /// Cookie collection for the current page.
    /// </summary>
    public sealed class CdpCookies
    {
        private readonly IList<CdpCookieItem> _items;

        internal CdpCookies(IList<CdpCookieItem> items)
        {
            _items = items ?? new List<CdpCookieItem>();
        }

        public string AsStr()
        {
            return string.Join("; ", _items.Select(item => item.Name + "=" + item.Value));
        }

        public IDictionary<string, string> AsDict()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var item in _items)
            {
                result[item.Name] = item.Value;
            }

            return result;
        }

        public string AsJson()
        {
            var serializer = new CdpJsonSerializer();
            return serializer.Serialize(_items);
        }
    }

    public sealed class CdpCookieItem
    {
        public CdpCookieItem(string name, string value)
        {
            Name = name ?? string.Empty;
            Value = value ?? string.Empty;
        }

        public string Name { get; private set; }

        public string Value { get; private set; }
    }
}
