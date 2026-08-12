using System;
using System.Collections.Concurrent;

namespace F2B
{
    /// <summary>
    /// Process-wide key/value bag for OpenRPA expressions and activities.
    /// Lives for the OpenRPA process lifetime (values can persist across runs unless cleared).
    /// Expression: <c>F2B.Global.Get("keyname")</c> / <c>F2B.Global.Set("keyname", value)</c> /
    /// <c>F2B.Global.Clear()</c>.
    /// </summary>
    public static class Global
    {
        private static readonly ConcurrentDictionary<string, object> Values =
            new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        public static object Get(string keyname)
        {
            if (string.IsNullOrWhiteSpace(keyname))
            {
                throw new ArgumentException("Key name is required.", nameof(keyname));
            }

            object value;
            return Values.TryGetValue(keyname.Trim(), out value) ? value : null;
        }

        /// <summary>
        /// Sets a value and returns null so OpenRPA expressions can use Set in expression slots.
        /// </summary>
        public static object Set(string keyname, object value)
        {
            if (string.IsNullOrWhiteSpace(keyname))
            {
                throw new ArgumentException("Key name is required.", nameof(keyname));
            }

            Values[keyname.Trim()] = value;
            return null;
        }

        public static bool Contains(string keyname)
        {
            return !string.IsNullOrWhiteSpace(keyname) && Values.ContainsKey(keyname.Trim());
        }

        public static bool Remove(string keyname)
        {
            if (string.IsNullOrWhiteSpace(keyname))
            {
                return false;
            }

            object removed;
            return Values.TryRemove(keyname.Trim(), out removed);
        }

        /// <summary>
        /// Clears the entire Global dictionary. Returns null for expression use.
        /// </summary>
        public static object Clear()
        {
            Values.Clear();
            return null;
        }
    }
}
