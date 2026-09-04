using System;
using System.Activities;
using System.Activities.Presentation;
using System.Activities.Presentation.Toolbox;
using System.Activities.Statements;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
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
        /// <summary>Type-level [Description] text used by palette search.</summary>
        public string Description { get; set; }

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

        /// <summary>
        /// Mirrors OpenRPA WFToolbox.InitializeActivitiesToolbox filters so Ctrl+P matches the toolbox.
        /// </summary>
        private static bool IsActivityType(Type activityType)
        {
            if (activityType == null || !activityType.IsVisible || !activityType.IsPublic || activityType.IsNested || activityType.IsAbstract)
            {
                return false;
            }

            try
            {
                if (activityType.Assembly != null
                    && activityType.Assembly.IsDynamic)
                {
                    return false;
                }

                string codeBase = activityType.Assembly?.CodeBase;
                if (!string.IsNullOrEmpty(codeBase)
                    && codeBase.IndexOf("Snippets.dll", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return false;
                }
            }
            catch
            {
            }

            if (activityType.GetConstructor(Type.EmptyTypes) == null)
            {
                return false;
            }

            if (ExcludedNames.Contains(activityType.Name))
            {
                return false;
            }

            if (!IsToolboxActivityKind(activityType))
            {
                return false;
            }

            string name = activityType.Name ?? string.Empty;
            if (name.StartsWith("InvokeAction`", StringComparison.Ordinal)
                || name.StartsWith("InvokeFunc`", StringComparison.Ordinal)
                || name.StartsWith("Subtract`", StringComparison.Ordinal)
                || name.StartsWith("GreaterThan`", StringComparison.Ordinal)
                || name.StartsWith("GreaterThanOrEqual`", StringComparison.Ordinal)
                || name.StartsWith("LessThan`", StringComparison.Ordinal)
                || name.StartsWith("LessThanOrEqual`", StringComparison.Ordinal)
                || name.StartsWith("Literal`", StringComparison.Ordinal)
                || name.StartsWith("MultidimensionalArrayItemReference`", StringComparison.Ordinal)
                || name.StartsWith("Multiply`", StringComparison.Ordinal)
                || name.StartsWith("New`", StringComparison.Ordinal)
                || name.StartsWith("NewArray`", StringComparison.Ordinal)
                || name.StartsWith("Or`", StringComparison.Ordinal)
                || name.StartsWith("OrElse", StringComparison.Ordinal)
                || name.EndsWith("`2", StringComparison.Ordinal)
                || name.EndsWith("`3", StringComparison.Ordinal)
                || name == "ExcelActivity"
                || name == "ExcelActivityOf`1")
            {
                return false;
            }

            if (activityType.FullName != null
                && (activityType.FullName.EndsWith("Statements.DoWhile", StringComparison.Ordinal)
                    || activityType.FullName.EndsWith("Statements.While", StringComparison.Ordinal)))
            {
                return false;
            }

            return true;
        }

        private static bool IsToolboxActivityKind(Type activityType)
        {
            return activityType.IsSubclassOf(typeof(Activity))
                || activityType.IsSubclassOf(typeof(NativeActivity))
                || activityType.IsSubclassOf(typeof(DynamicActivity))
                || activityType.IsSubclassOf(typeof(ActivityWithResult))
                || activityType.IsSubclassOf(typeof(AsyncCodeActivity))
                || activityType.IsSubclassOf(typeof(CodeActivity))
                || activityType.IsSubclassOf(typeof(FlowNode))
                || activityType == typeof(State)
                || string.Equals(activityType.Name, "FinalState", StringComparison.Ordinal)
                || typeof(IActivityTemplateFactory).IsAssignableFrom(activityType);
        }

        /// <summary>Same naming rules as OpenRPA WFToolbox.getDisplayName.</summary>
        private static string ResolveDisplayName(Type type)
        {
            if (type == null)
            {
                return string.Empty;
            }

            string displayName = type.Name;
            string[] splitName = displayName.Split('`');
            displayName = splitName[0];
            try
            {
                var attr = type.GetCustomAttributes(typeof(DisplayNameAttribute), true)
                    .FirstOrDefault() as DisplayNameAttribute;
                if (attr != null && !string.IsNullOrWhiteSpace(attr.DisplayName))
                {
                    displayName = attr.DisplayName;
                }
            }
            catch
            {
            }

            if (splitName.Length > 1)
            {
                displayName = string.Format("{0}<>", displayName);
            }

            return displayName;
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
                return ActivitySearchHistory.GetRecentItems(all, maxResults);
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
            string nameCompact = CompactSearchText(name);
            string needleCompact = CompactSearchText(needle);

            if (string.Equals(name, needle, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrEmpty(nameCompact)
                    && string.Equals(nameCompact, needleCompact, StringComparison.OrdinalIgnoreCase)))
            {
                return 1000;
            }

            if (name.StartsWith(needle, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrEmpty(needleCompact)
                    && nameCompact.StartsWith(needleCompact, StringComparison.OrdinalIgnoreCase)))
            {
                return 800 - Math.Min(200, name.Length);
            }

            int idx = name.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                return 500 - idx;
            }

            if (!string.IsNullOrEmpty(needleCompact))
            {
                idx = nameCompact.IndexOf(needleCompact, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    return 480 - idx;
                }
            }

            if (library.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 200;
            }

            string description = item.Description ?? string.Empty;
            if (!string.IsNullOrEmpty(description))
            {
                int descIdx = description.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
                if (descIdx >= 0)
                {
                    return 180 - Math.Min(80, descIdx);
                }

                if (!string.IsNullOrEmpty(needleCompact))
                {
                    int compactIdx = CompactSearchText(description)
                        .IndexOf(needleCompact, StringComparison.OrdinalIgnoreCase);
                    if (compactIdx >= 0)
                    {
                        return 160 - Math.Min(60, compactIdx);
                    }
                }
            }

            // Full type name only as a weak exact substring (avoid fuzzy noise on long namespaces).
            if (full.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0
                || (!string.IsNullOrEmpty(needleCompact)
                    && CompactSearchText(full).IndexOf(needleCompact, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return 100;
            }

            // Token / camel-friendly on display name only
            if (FuzzyContains(name, needle)
                || (!string.IsNullOrEmpty(needleCompact) && FuzzyContains(nameCompact, needleCompact)))
            {
                return 50;
            }

            return 0;
        }

        private static bool LooksLikeTypeFullName(string text, Type type)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (type != null)
            {
                if (string.Equals(text, type.FullName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(text, type.Name, StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrEmpty(type.FullName)
                        && text.StartsWith(type.Namespace + ".", StringComparison.OrdinalIgnoreCase)
                        && text.IndexOf('.') >= 0))
                {
                    // type.Name alone is OK as fallback display; only treat dotted names as "too complete".
                    return text.IndexOf('.') >= 0;
                }
            }

            return text.IndexOf('.') >= 0 && text.EndsWith("Activity", StringComparison.OrdinalIgnoreCase);
        }

        private static string CompactSearchText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (char.IsWhiteSpace(ch) || ch == '<' || ch == '>' || ch == '`')
                {
                    continue;
                }

                sb.Append(ch);
            }

            return sb.ToString();
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
                        LibraryName = ResolveLibraryName(type),
                        Description = ResolveDescription(type)
                    });
                }
            }

            MergeFromLiveToolbox(result, seen);

            return result
                .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.LibraryName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Picks up dynamic toolbox entries (e.g. OpenRPA.Script) that are not in exported types scan.
        /// </summary>
        private static void MergeFromLiveToolbox(List<ActivityCatalogItem> result, HashSet<string> seen)
        {
            ToolboxControl toolbox;
            try
            {
                toolbox = ToolboxAccess.FindToolboxControl();
            }
            catch
            {
                return;
            }

            if (toolbox?.Categories == null)
            {
                return;
            }

            foreach (ToolboxCategory category in toolbox.Categories)
            {
                if (category?.Tools == null)
                {
                    continue;
                }

                string libraryName = category.CategoryName ?? string.Empty;
                foreach (ToolboxItemWrapper wrapper in category.Tools)
                {
                    Type type = wrapper?.Type;
                    if (type == null || !IsActivityType(type))
                    {
                        continue;
                    }

                    string key = type.FullName ?? type.Name;
                    // ToolName is the type identity (often FullName); DisplayName is the toolbox label.
                    string displayName = wrapper.DisplayName;
                    if (string.IsNullOrWhiteSpace(displayName)
                        || LooksLikeTypeFullName(displayName, type))
                    {
                        displayName = ResolveDisplayName(type);
                    }

                    ActivityCatalogItem existing = result.FirstOrDefault(
                        item => string.Equals(item.FullName, key, StringComparison.Ordinal));
                    if (existing != null)
                    {
                        if (!string.IsNullOrWhiteSpace(displayName)
                            && !LooksLikeTypeFullName(displayName, type))
                        {
                            existing.DisplayName = displayName;
                        }

                        if (string.IsNullOrWhiteSpace(existing.Description))
                        {
                            existing.Description = ResolveDescription(type);
                        }

                        continue;
                    }

                    if (!seen.Add(key))
                    {
                        continue;
                    }

                    result.Add(new ActivityCatalogItem
                    {
                        Type = type,
                        DisplayName = displayName,
                        FullName = key,
                        LibraryName = string.IsNullOrWhiteSpace(libraryName)
                            ? ResolveLibraryName(type)
                            : libraryName,
                        Description = ResolveDescription(type)
                    });
                }
            }
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

        private static string ResolveDescription(Type type)
        {
            if (type == null)
            {
                return string.Empty;
            }

            try
            {
                var attr = type.GetCustomAttributes(typeof(DescriptionAttribute), true)
                    .FirstOrDefault() as DescriptionAttribute;
                if (attr != null && !string.IsNullOrWhiteSpace(attr.Description))
                {
                    return attr.Description.Trim();
                }
            }
            catch
            {
            }

            return string.Empty;
        }
    }
}
