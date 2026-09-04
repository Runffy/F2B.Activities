using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenRPA.Interfaces;

namespace OpenRPA.PluginFunctions
{
    /// <summary>
    /// Scans Libs: root *.xaml → Customized.Uncategorized; first-level folder *.xaml → Customized.{folder}.
    /// Deeper paths are ignored in phase 1.
    /// </summary>
    internal static class LibXamlScanner
    {
        public static IReadOnlyList<LibXamlEntry> Scan()
        {
            var result = new List<LibXamlEntry>();
            string root = LibXamlPaths.GetLibsRoot();
            if (!Directory.Exists(root))
            {
                Log.Information("PluginFunctions: Libs folder not found: " + root);
                return result;
            }

            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string file in Directory.GetFiles(root, "*.xaml", SearchOption.TopDirectoryOnly))
            {
                TryAdd(result, seenKeys, file, LibXamlPaths.UncategorizedCategory, "Uncategorized");
            }

            foreach (string dir in Directory.GetDirectories(root))
            {
                string folderName = Path.GetFileName(dir);
                if (string.IsNullOrWhiteSpace(folderName)
                    || folderName.StartsWith(".", StringComparison.Ordinal))
                {
                    continue;
                }

                string category = LibXamlPaths.ToCategoryName(folderName);
                foreach (string file in Directory.GetFiles(dir, "*.xaml", SearchOption.TopDirectoryOnly))
                {
                    TryAdd(result, seenKeys, file, category, folderName);
                }
            }

            Log.Information(
                "PluginFunctions: scanned " + result.Count + " Lib XAML(s) under " + root);
            return result;
        }

        private static void TryAdd(
            List<LibXamlEntry> result,
            HashSet<string> seenKeys,
            string filePath,
            string category,
            string folderKey)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return;
            }

            string displayName = Path.GetFileNameWithoutExtension(filePath);
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return;
            }

            string typeKey = SanitizeIdentifier(folderKey) + "_" + SanitizeIdentifier(displayName);
            if (!seenKeys.Add(typeKey))
            {
                typeKey = typeKey + "_" + seenKeys.Count.ToString();
                seenKeys.Add(typeKey);
            }

            result.Add(new LibXamlEntry
            {
                FilePath = filePath,
                DisplayName = displayName.Trim(),
                Category = category,
                TypeKey = typeKey
            });
        }

        private static string SanitizeIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "Item";
            }

            var sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                if (char.IsLetterOrDigit(ch) || ch == '_')
                {
                    sb.Append(ch);
                }
                else
                {
                    sb.Append('_');
                }
            }

            if (sb.Length == 0 || char.IsDigit(sb[0]))
            {
                sb.Insert(0, 'L');
            }

            return sb.ToString();
        }
    }
}
