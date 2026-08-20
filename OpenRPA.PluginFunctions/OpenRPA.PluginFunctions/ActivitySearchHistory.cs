using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenRPA.PluginFunctions
{
    /// <summary>
    /// Local persistence for Activity Searcher (Ctrl+P) recent picks.
    /// </summary>
    internal static class ActivitySearchHistory
    {
        private const int MaxEntries = 40;
        private static readonly object Gate = new object();
        private static List<string> _recentFullNames;
        private static bool _loaded;

        public static void Record(Type activityType)
        {
            if (activityType == null)
            {
                return;
            }

            string key = activityType.FullName;
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            lock (Gate)
            {
                EnsureLoaded();
                _recentFullNames.RemoveAll(x => string.Equals(x, key, StringComparison.OrdinalIgnoreCase));
                _recentFullNames.Insert(0, key);
                while (_recentFullNames.Count > MaxEntries)
                {
                    _recentFullNames.RemoveAt(_recentFullNames.Count - 1);
                }

                SaveUnlocked();
            }
        }

        public static IEnumerable<ActivityCatalogItem> GetRecentItems(
            IReadOnlyList<ActivityCatalogItem> catalog,
            int maxResults)
        {
            if (catalog == null || catalog.Count == 0 || maxResults <= 0)
            {
                return Enumerable.Empty<ActivityCatalogItem>();
            }

            List<string> recent;
            lock (Gate)
            {
                EnsureLoaded();
                recent = _recentFullNames.ToList();
            }

            var byFullName = new Dictionary<string, ActivityCatalogItem>(StringComparer.OrdinalIgnoreCase);
            foreach (ActivityCatalogItem item in catalog)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.FullName))
                {
                    continue;
                }

                if (!byFullName.ContainsKey(item.FullName))
                {
                    byFullName[item.FullName] = item;
                }
            }

            var result = new List<ActivityCatalogItem>();
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string fullName in recent)
            {
                ActivityCatalogItem item;
                if (!byFullName.TryGetValue(fullName, out item) || item == null)
                {
                    continue;
                }

                if (!used.Add(item.FullName))
                {
                    continue;
                }

                result.Add(item);
                if (result.Count >= maxResults)
                {
                    return result;
                }
            }

            foreach (ActivityCatalogItem item in catalog)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.FullName))
                {
                    continue;
                }

                if (!used.Add(item.FullName))
                {
                    continue;
                }

                result.Add(item);
                if (result.Count >= maxResults)
                {
                    break;
                }
            }

            return result;
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            _recentFullNames = new List<string>();
            try
            {
                string path = GetStorePath();
                if (!File.Exists(path))
                {
                    return;
                }

                foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    string name = (line ?? string.Empty).Trim();
                    if (string.IsNullOrEmpty(name)
                        || _recentFullNames.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    _recentFullNames.Add(name);
                    if (_recentFullNames.Count >= MaxEntries)
                    {
                        break;
                    }
                }
            }
            catch
            {
                _recentFullNames = new List<string>();
            }
        }

        private static void SaveUnlocked()
        {
            try
            {
                string path = GetStorePath();
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllLines(path, _recentFullNames.ToArray(), Encoding.UTF8);
            }
            catch
            {
            }
        }

        private static string GetStorePath()
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(root, "OpenRPA", "PluginFunctions", "activity-palette-recents.txt");
        }
    }
}
