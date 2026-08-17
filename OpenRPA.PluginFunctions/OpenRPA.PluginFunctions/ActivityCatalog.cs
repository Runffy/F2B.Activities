using System;
using System.Activities;
using System.Activities.Presentation;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows.Media;
using OpenRPA.Interfaces;

namespace OpenRPA.PluginFunctions
{
    internal sealed class ActivityCatalogItem
    {
        private ImageSource _icon;
        private bool _iconResolved;

        public Type Type { get; set; }
        public string DisplayName { get; set; }
        public string FullName { get; set; }
        /// <summary>Assembly / toolbox category name, e.g. OpenRPA.SAP.</summary>
        public string LibraryName { get; set; }

        public ImageSource Icon
        {
            get
            {
                if (!_iconResolved)
                {
                    _icon = ActivityIconResolver.Resolve(Type);
                    // Keep retrying until an icon is found (toolbox may not be ready yet).
                    if (_icon != null)
                    {
                        _iconResolved = true;
                    }
                }

                return _icon;
            }
        }
    }

    /// <summary>
    /// Discovers public activity types from loaded assemblies (same spirit as OpenRPA toolbox).
    /// </summary>
    internal static class ActivityCatalog
    {
        private static List<ActivityCatalogItem> _cache;
        private static readonly object Gate = new object();

        private static readonly HashSet<string> ExcludedNames = new HashSet<string>(StringComparer.Ordinal)
        {
            // Align with OpenRPA WFToolbox excludeActivities (non-droppable / infra types).
            "AddValidationError", "AndAlso", "AssertValidation", "CreateBookmarkScope", "DeleteBookmarkScope",
            "DynamicActivity", "CancellationScope", "CompensableActivity", "Compensate", "Confirm",
            "GetChildSubtree", "GetParentChain", "GetWorkflowTree",
            "Add`3", "And`3", "As`2", "Cast`2", "ArgumentValue`1", "ArrayItemReference`1", "ArrayItemValue`1",
            "Assign`1", "Constraint`1", "CSharpReference`1", "CSharpValue`1", "DelegateArgumentReference`1",
            "DelegateArgumentValue`1", "Divide`3", "DynamicActivity`1", "Equal`3", "FieldReference`2", "FieldValue`2",
            "ForEach`1", "InvokeAction", "InvokeDelegate", "ArgumentReference`1", "VariableReference`1",
            "VariableValue`1", "VisualBasicReference`1", "VisualBasicValue`1", "InvokeMethod`1",
            "StateMachineWithInitialStateFactory", "ParallelForEach`1", "ForEachWithBodyFactory`1",
            "ExcelActivity", "ExcelActivityOf`1"
        };

        private static bool IsActivityType(Type type)
        {
            if (type == null || !type.IsVisible || !type.IsPublic || type.IsNested || type.IsAbstract)
            {
                return false;
            }

            // Open generics (e.g. Assign`1 → shown as Assign<>) cannot be dropped without type args.
            if (type.IsGenericTypeDefinition || type.ContainsGenericParameters)
            {
                return false;
            }

            if (type.GetConstructor(Type.EmptyTypes) == null)
            {
                return false;
            }

            if (ExcludedNames.Contains(type.Name))
            {
                return false;
            }

            // Any leftover open-generic naming (Assign`1, InvokeFunc`2, ...).
            if (type.Name.IndexOf('`') >= 0)
            {
                return false;
            }

            if (type.Name.StartsWith("InvokeAction", StringComparison.Ordinal)
                || type.Name.StartsWith("InvokeFunc", StringComparison.Ordinal))
            {
                return false;
            }

            if (type.FullName != null
                && (type.FullName.EndsWith("Statements.DoWhile", StringComparison.Ordinal)
                    || type.FullName.EndsWith("Statements.While", StringComparison.Ordinal)))
            {
                return false;
            }

            return typeof(Activity).IsAssignableFrom(type)
                   || typeof(IActivityTemplateFactory).IsAssignableFrom(type);
        }

        private static string ResolveDisplayName(Type type)
        {
            try
            {
                var attr = type.GetCustomAttributes(typeof(DisplayNameAttribute), true)
                    .FirstOrDefault() as DisplayNameAttribute;
                if (attr != null && !string.IsNullOrWhiteSpace(attr.DisplayName))
                {
                    return attr.DisplayName;
                }
            }
            catch
            {
            }

            return type.Name;
        }

        public static IReadOnlyList<ActivityCatalogItem> GetAll()
        {
            lock (Gate)
            {
                if (_cache != null)
                {
                    return _cache;
                }

                _cache = Build();
                return _cache;
            }
        }

        public static void Invalidate()
        {
            lock (Gate)
            {
                _cache = null;
            }
        }

        public static IEnumerable<ActivityCatalogItem> Search(string pattern, int maxResults = 40)
        {
            string needle = (pattern ?? string.Empty).Trim();
            IReadOnlyList<ActivityCatalogItem> all = GetAll();
            if (needle.Length == 0)
            {
                return all.Take(maxResults);
            }

            return all
                .Select(item => new { Item = item, Score = Score(item, needle) })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Item.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Take(maxResults)
                .Select(x => x.Item);
        }

        private static int Score(ActivityCatalogItem item, string needle)
        {
            if (item == null || string.IsNullOrEmpty(needle))
            {
                return 0;
            }

            string name = item.DisplayName ?? string.Empty;
            string full = item.FullName ?? string.Empty;
            string library = item.LibraryName ?? string.Empty;
            if (string.Equals(name, needle, StringComparison.OrdinalIgnoreCase))
            {
                return 1000;
            }

            if (name.StartsWith(needle, StringComparison.OrdinalIgnoreCase))
            {
                return 800 - Math.Min(200, name.Length);
            }

            int idx = name.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                return 500 - idx;
            }

            if (library.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 200;
            }

            if (full.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 100;
            }

            // Token / camel-friendly: all chars appear in order
            if (FuzzyContains(name, needle))
            {
                return 50;
            }

            return 0;
        }

        private static bool FuzzyContains(string text, string needle)
        {
            int t = 0;
            for (int n = 0; n < needle.Length; n++)
            {
                char c = char.ToLowerInvariant(needle[n]);
                bool found = false;
                while (t < text.Length)
                {
                    if (char.ToLowerInvariant(text[t++]) == c)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    return false;
                }
            }

            return true;
        }

        private static List<ActivityCatalogItem> Build()
        {
            var result = new List<ActivityCatalogItem>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies().OrderBy(a => a.GetName().Name))
            {
                if (assembly.IsDynamic)
                {
                    continue;
                }

                string asmName = assembly.GetName().Name ?? string.Empty;
                if (string.Equals(asmName, "System.ServiceModel.Activities", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Type[] types;
                try
                {
                    types = assembly.GetExportedTypes();
                }
                catch
                {
                    continue;
                }

                foreach (Type type in types)
                {
                    if (!IsActivityType(type))
                    {
                        continue;
                    }

                    string key = type.FullName ?? type.Name;
                    if (!seen.Add(key))
                    {
                        continue;
                    }

                    result.Add(new ActivityCatalogItem
                    {
                        Type = type,
                        DisplayName = ResolveDisplayName(type),
                        FullName = key,
                        LibraryName = ResolveLibraryName(type)
                    });
                }
            }

            return result
                .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.LibraryName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string ResolveLibraryName(Type type)
        {
            if (type == null || type.Assembly == null)
            {
                return string.Empty;
            }

            try
            {
                string name = type.Assembly.GetName().Name;
                return string.IsNullOrWhiteSpace(name) ? string.Empty : name;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
