using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using F2B.Basic;

namespace F2B
{
    /// <summary>
    /// Per-run key/value bag for OpenRPA expressions and activities.
    /// Scoped to the outermost source workflow InstanceId (Invoke OpenRPA caller chain),
    /// so values do not leak across separate OpenRPA runs in the same process.
    /// Expression: <c>F2B.Global.Get("keyname")</c> / <c>F2B.Global.Set("keyname", value)</c>.
    /// </summary>
    public static class Global
    {
        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, object>> BagsBySourceInstanceId =
            new ConcurrentDictionary<string, ConcurrentDictionary<string, object>>(StringComparer.OrdinalIgnoreCase);

        public static object Get(string keyname)
        {
            return GetFromBag(CurrentBag(), keyname);
        }

        public static object Get(System.Activities.CodeActivityContext context, string keyname)
        {
            return GetFromBag(BagFor(RuntimeDirectory.ResolveSourceInstanceId(context)), keyname);
        }

        /// <summary>
        /// Sets a value and returns null so OpenRPA expressions can use Set in expression slots.
        /// </summary>
        public static object Set(string keyname, object value)
        {
            return SetInBag(CurrentBag(), keyname, value);
        }

        public static object Set(System.Activities.CodeActivityContext context, string keyname, object value)
        {
            return SetInBag(BagFor(RuntimeDirectory.ResolveSourceInstanceId(context)), keyname, value);
        }

        public static bool Contains(string keyname)
        {
            return !string.IsNullOrWhiteSpace(keyname) && CurrentBag().ContainsKey(keyname.Trim());
        }

        public static bool Remove(string keyname)
        {
            if (string.IsNullOrWhiteSpace(keyname))
            {
                return false;
            }

            object removed;
            return CurrentBag().TryRemove(keyname.Trim(), out removed);
        }

        /// <summary>
        /// Clears the bag for the current source workflow run only.
        /// </summary>
        public static void Clear()
        {
            CurrentBag().Clear();
        }

        /// <summary>
        /// Clears all run-scoped bags (all OpenRPA runs in this process).
        /// </summary>
        public static void ClearAll()
        {
            BagsBySourceInstanceId.Clear();
        }

        private static ConcurrentDictionary<string, object> CurrentBag()
        {
            return BagFor(RuntimeDirectory.ResolveSourceInstanceId());
        }

        private static ConcurrentDictionary<string, object> BagFor(string sourceInstanceId)
        {
            PruneStaleBags();

            string key = string.IsNullOrWhiteSpace(sourceInstanceId)
                ? string.Empty
                : sourceInstanceId.Trim();

            return BagsBySourceInstanceId.GetOrAdd(
                key,
                _ => new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase));
        }

        private static void PruneStaleBags()
        {
            try
            {
                HashSet<string> active = RuntimeDirectory.GetActiveSourceInstanceIds();
                foreach (string key in BagsBySourceInstanceId.Keys)
                {
                    if (string.IsNullOrEmpty(key))
                    {
                        // Fallback bag when InstanceId cannot be resolved — drop when any real run is active.
                        if (active.Count > 0)
                        {
                            ConcurrentDictionary<string, object> unused;
                            BagsBySourceInstanceId.TryRemove(key, out unused);
                        }

                        continue;
                    }

                    if (!active.Contains(key))
                    {
                        ConcurrentDictionary<string, object> unused;
                        BagsBySourceInstanceId.TryRemove(key, out unused);
                    }
                }
            }
            catch
            {
                // Keep Global usable even if OpenRPA instance enumeration fails.
            }
        }

        private static object GetFromBag(ConcurrentDictionary<string, object> bag, string keyname)
        {
            if (string.IsNullOrWhiteSpace(keyname))
            {
                throw new ArgumentException("Key name is required.", nameof(keyname));
            }

            object value;
            return bag.TryGetValue(keyname.Trim(), out value) ? value : null;
        }

        private static object SetInBag(ConcurrentDictionary<string, object> bag, string keyname, object value)
        {
            if (string.IsNullOrWhiteSpace(keyname))
            {
                throw new ArgumentException("Key name is required.", nameof(keyname));
            }

            bag[keyname.Trim()] = value;
            return null;
        }
    }
}
