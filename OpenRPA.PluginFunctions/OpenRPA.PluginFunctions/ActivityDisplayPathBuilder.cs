using System;
using System.Activities;
using System.Activities.Presentation.Model;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenRPA.PluginFunctions
{
    /// <summary>
    /// Builds DisplayName breadcrumb paths in the same spirit as F2B.Basic TraceableTryCatch fault paths:
    /// start from the outermost Sequence (or Flowchart / StateMachine), segments joined by '/',
    /// and same-DisplayName siblings get a 1-based index with [1] omitted.
    /// Designer wrappers (ValidatingCollection, ActivityAction`1, …) are excluded.
    /// </summary>
    internal static class ActivityDisplayPathBuilder
    {
        private static readonly HashSet<string> SkipTypeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ValidatingCollection", "ModelItemCollection", "Collection", "Dictionary",
            "ActivityAction", "ActivityFunc", "ActivityDelegate",
            "DelegateInArgument", "DelegateOutArgument",
            "DynamicActivity", "ActivityBuilder",
            "Variable", "Variable`1", "RuntimeArgument",
            "FlowNode", "FlowStep", "FlowDecision", "FlowSwitch",
            "Catch", "Catch`1" // Catch&lt;T&gt; wrapper; the inner Sequence named "Catch" is kept via DisplayName
        };

        public static string BuildFromModelItem(ModelItem item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            var chain = new List<ModelItem>();
            ModelItem current = item;
            int guard = 0;
            while (current != null && guard++ < 512)
            {
                if (IsPathSegmentItem(current))
                {
                    chain.Insert(0, current);
                }

                current = current.Parent;
            }

            return FormatChain(chain);
        }

        /// <summary>
        /// Formats an already-ordered root→leaf DisplayName chain (e.g. from XAML path stack).
        /// Trims designer junk and starts at the outermost Sequence when present.
        /// </summary>
        public static string FormatDisplayNameChain(IList<string> displayNames)
        {
            if (displayNames == null || displayNames.Count == 0)
            {
                return string.Empty;
            }

            var cleaned = new List<string>();
            foreach (string raw in displayNames)
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                string segment = SanitizePathSegment(raw.Trim());
                if (segment.Length == 0 || IsJunkSegment(segment))
                {
                    continue;
                }

                // Collapse consecutive duplicates (e.g. Try/Try from section + inner Sequence).
                if (cleaned.Count > 0
                    && string.Equals(cleaned[cleaned.Count - 1], segment, StringComparison.Ordinal))
                {
                    continue;
                }

                cleaned.Add(segment);
            }

            if (cleaned.Count == 0)
            {
                return string.Empty;
            }

            int start = FindCanvasRootIndex(cleaned);
            var sb = new StringBuilder();
            for (int i = start; i < cleaned.Count; i++)
            {
                if (sb.Length > 0)
                {
                    sb.Append('/');
                }

                sb.Append(cleaned[i]);
            }

            return sb.ToString();
        }

        private static string FormatChain(IList<ModelItem> chain)
        {
            if (chain == null || chain.Count == 0)
            {
                return string.Empty;
            }

            int start = 0;
            for (int i = 0; i < chain.Count; i++)
            {
                if (IsCanvasRootType(chain[i].ItemType))
                {
                    start = i;
                    break;
                }
            }

            var sb = new StringBuilder();
            string previous = null;
            for (int i = start; i < chain.Count; i++)
            {
                ModelItem node = chain[i];
                string segment = SanitizePathSegment(GetExplicitDisplayName(node));
                if (string.IsNullOrWhiteSpace(segment) || IsJunkSegment(segment))
                {
                    continue;
                }

                if (string.Equals(previous, segment, StringComparison.Ordinal))
                {
                    continue;
                }

                if (sb.Length > 0)
                {
                    sb.Append('/');
                }

                sb.Append(segment);

                int index1Based = GetSameDisplayNameIndex1Based(node);
                int sameNameCount = CountSameDisplayNameSiblings(node);
                if (sameNameCount > 1 && index1Based > 1)
                {
                    sb.Append('[');
                    sb.Append(index1Based);
                    sb.Append(']');
                }

                previous = segment;
            }

            return sb.ToString();
        }

        private static int FindCanvasRootIndex(IList<string> names)
        {
            for (int i = 0; i < names.Count; i++)
            {
                string n = names[i];
                if (string.Equals(n, "Sequence", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(n, "Flowchart", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(n, "StateMachine", StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return 0;
        }

        private static bool IsPathSegmentItem(ModelItem item)
        {
            if (item == null || item.ItemType == null)
            {
                return false;
            }

            Type type = item.ItemType;
            if (IsSkippedType(type))
            {
                return false;
            }

            // Only real activities with an explicit DisplayName (never fall back to Type.Name).
            if (!typeof(Activity).IsAssignableFrom(type))
            {
                return false;
            }

            string name = GetExplicitDisplayName(item);
            return !string.IsNullOrWhiteSpace(name) && !IsJunkSegment(name);
        }

        private static string GetExplicitDisplayName(ModelItem item)
        {
            if (item == null)
            {
                return null;
            }

            try
            {
                ModelProperty prop = item.Properties["DisplayName"];
                if (prop != null && prop.ComputedValue != null)
                {
                    string text = Convert.ToString(prop.ComputedValue);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text.Trim();
                    }
                }

                object current = item.GetCurrentValue();
                var activity = current as Activity;
                if (activity != null && !string.IsNullOrWhiteSpace(activity.DisplayName))
                {
                    return activity.DisplayName.Trim();
                }
            }
            catch
            {
            }

            return null;
        }

        private static bool IsSkippedType(Type type)
        {
            if (type == null)
            {
                return true;
            }

            if (typeof(DynamicActivity).IsAssignableFrom(type))
            {
                return true;
            }

            string name = type.Name ?? string.Empty;
            int tick = name.IndexOf('`');
            string bare = tick > 0 ? name.Substring(0, tick) : name;

            if (SkipTypeNames.Contains(name) || SkipTypeNames.Contains(bare))
            {
                return true;
            }

            // ActivityAction`1 / ActivityFunc`2 — delegate wrappers in TryCatch / ForEach.
            if (bare.StartsWith("ActivityAction", StringComparison.OrdinalIgnoreCase)
                || bare.StartsWith("ActivityFunc", StringComparison.OrdinalIgnoreCase)
                || bare.StartsWith("ActivityDelegate", StringComparison.OrdinalIgnoreCase)
                || bare.IndexOf("Collection", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            // Catch&lt;T&gt; metadata wrapper (inner body Sequence still contributes "Catch"/"Try").
            if (string.Equals(bare, "Catch", StringComparison.OrdinalIgnoreCase)
                && type.IsGenericType)
            {
                return true;
            }

            return false;
        }

        private static bool IsJunkSegment(string segment)
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                return true;
            }

            // CLR generic type names / XAML property elements leaking into the path.
            if (segment.IndexOf('`') >= 0 || segment.IndexOf('.') >= 0)
            {
                return true;
            }

            if (segment.StartsWith("Validating", StringComparison.OrdinalIgnoreCase)
                || segment.StartsWith("ActivityAction", StringComparison.OrdinalIgnoreCase)
                || segment.StartsWith("ActivityFunc", StringComparison.OrdinalIgnoreCase)
                || segment.Equals("ActivityDelegate", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static bool IsCanvasRootType(Type type)
        {
            if (type == null)
            {
                return false;
            }

            string name = type.Name ?? string.Empty;
            int tick = name.IndexOf('`');
            if (tick > 0)
            {
                name = name.Substring(0, tick);
            }

            return string.Equals(name, "Sequence", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Flowchart", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "StateMachine", StringComparison.OrdinalIgnoreCase);
        }

        private static int GetSameDisplayNameIndex1Based(ModelItem item)
        {
            ModelItem parent = FindActivityParent(item);
            if (parent == null || item == null)
            {
                return 1;
            }

            string name = GetExplicitDisplayName(item);
            int index = 0;
            foreach (ModelItem sibling in EnumerateDirectActivityChildren(parent))
            {
                if (!string.Equals(GetExplicitDisplayName(sibling), name, StringComparison.Ordinal))
                {
                    continue;
                }

                index++;
                if (ReferenceEquals(sibling, item))
                {
                    return index;
                }
            }

            return 1;
        }

        private static int CountSameDisplayNameSiblings(ModelItem item)
        {
            ModelItem parent = FindActivityParent(item);
            if (parent == null || item == null)
            {
                return 1;
            }

            string name = GetExplicitDisplayName(item);
            int count = 0;
            foreach (ModelItem sibling in EnumerateDirectActivityChildren(parent))
            {
                if (string.Equals(GetExplicitDisplayName(sibling), name, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count <= 0 ? 1 : count;
        }

        private static ModelItem FindActivityParent(ModelItem item)
        {
            ModelItem current = item != null ? item.Parent : null;
            int guard = 0;
            while (current != null && guard++ < 64)
            {
                if (IsPathSegmentItem(current))
                {
                    return current;
                }

                current = current.Parent;
            }

            return null;
        }

        private static IEnumerable<ModelItem> EnumerateDirectActivityChildren(ModelItem parent)
        {
            if (parent == null || parent.Properties == null)
            {
                yield break;
            }

            string[] propertyNames =
            {
                "Activities", "Nodes", "States",
                "Body", "Handler", "Action",
                "Try", "Finally", "Then", "Else",
                "Catches"
            };

            var yielded = new HashSet<ModelItem>();
            foreach (string propName in propertyNames)
            {
                ModelProperty prop = null;
                try
                {
                    prop = parent.Properties[propName];
                }
                catch
                {
                    prop = null;
                }

                if (prop == null)
                {
                    continue;
                }

                if (prop.IsCollection && prop.Collection != null)
                {
                    foreach (ModelItem child in prop.Collection)
                    {
                        if (child == null || !yielded.Add(child))
                        {
                            continue;
                        }

                        if (IsPathSegmentItem(child))
                        {
                            yield return child;
                        }
                        else
                        {
                            foreach (ModelItem nested in EnumerateDirectActivityChildren(child))
                            {
                                if (nested != null && yielded.Add(nested))
                                {
                                    yield return nested;
                                }
                            }
                        }
                    }
                }
                else if (prop.Value != null)
                {
                    ModelItem child = prop.Value;
                    if (!yielded.Add(child))
                    {
                        continue;
                    }

                    if (IsPathSegmentItem(child))
                    {
                        yield return child;
                    }
                    else
                    {
                        foreach (ModelItem nested in EnumerateDirectActivityChildren(child))
                        {
                            if (nested != null && yielded.Add(nested))
                            {
                                yield return nested;
                            }
                        }
                    }
                }
            }
        }

        private static string SanitizePathSegment(string segment)
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                return string.Empty;
            }

            return segment
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Replace('/', '·')
                .Trim();
        }
    }
}
