using System;
using System.Activities;
using System.Activities.Statements;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text;

namespace F2B.Basic
{
    /// <summary>
    /// Builds a Try-relative structural path (XPath-like) and a DisplayName breadcrumb for a faulted activity.
    /// Prefers a Tracking-provided activity Id (first Faulted / last Executing), then runtime instance tree.
    /// </summary>
    internal static class ActivityFaultPathBuilder
    {
        public const string DataKeyActivityId = "F2B.FaultActivityId";
        public const string DataKeyDisplayName = "F2B.FaultDisplayName";
        public const string DataKeyXPath = "F2B.FaultXPath";
        public const string DataKeyDisplayPath = "F2B.FaultDisplayPath";

        private static readonly FieldInfo ChildListField = typeof(ActivityInstance).GetField(
            "childList",
            BindingFlags.Instance | BindingFlags.NonPublic);

        public sealed class Result
        {
            public string ActivityId { get; set; }
            public string DisplayName { get; set; }
            public string XPath { get; set; }
            public string DisplayPath { get; set; }

            /// <summary>
            /// Human-oriented path for Exception.Source: DisplayName segments with
            /// 0-based index among same-DisplayName siblings (omitted when unique).
            /// </summary>
            public string SourcePath
            {
                get
                {
                    if (!string.IsNullOrWhiteSpace(DisplayPath))
                    {
                        return DisplayPath;
                    }

                    string xpath = XPath ?? string.Empty;
                    if (xpath.StartsWith("//", StringComparison.Ordinal))
                    {
                        return xpath.Substring(2);
                    }

                    return xpath;
                }
            }
        }

        public static Result Build(Activity tryRoot, ActivityInstance propagatedFrom, string preferredActivityId = null)
        {
            Activity leaf = null;

            if (!string.IsNullOrEmpty(preferredActivityId))
            {
                leaf = FindActivityById(tryRoot, preferredActivityId);
            }

            if (leaf == null)
            {
                ActivityInstance leafInstance = ResolveFaultLeaf(propagatedFrom);
                leaf = leafInstance != null ? leafInstance.Activity : null;
            }

            // If we still only have the Try root, try last-known executing via preferred id already failed —
            // walk instance tree without requiring Faulted state (take deepest child chain).
            if (leaf != null && tryRoot != null && ReferenceEquals(leaf, tryRoot) && propagatedFrom != null)
            {
                ActivityInstance deepest = FindDeepestDescendant(propagatedFrom);
                if (deepest != null && deepest.Activity != null && !ReferenceEquals(deepest.Activity, tryRoot))
                {
                    leaf = deepest.Activity;
                }
            }

            List<Activity> chain = FindPath(tryRoot, leaf);
            if (chain == null || chain.Count == 0)
            {
                if (leaf != null)
                {
                    chain = new List<Activity> { leaf };
                }
                else
                {
                    return EmptyResult();
                }
            }

            Activity resultLeaf = chain[chain.Count - 1];
            return new Result
            {
                ActivityId = resultLeaf != null ? resultLeaf.Id ?? string.Empty : string.Empty,
                DisplayName = GetDisplayLabel(resultLeaf),
                XPath = BuildXPath(chain),
                DisplayPath = BuildDisplayPath(chain)
            };
        }

        public static void EnrichException(Exception exception, Result fault)
        {
            if (exception == null || fault == null)
            {
                return;
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(fault.SourcePath))
                {
                    exception.Source = fault.SourcePath;
                }
                else if (!string.IsNullOrWhiteSpace(fault.DisplayPath))
                {
                    exception.Source = fault.DisplayPath;
                }
                else if (!string.IsNullOrWhiteSpace(fault.DisplayName))
                {
                    exception.Source = fault.DisplayName;
                }

                exception.Data[DataKeyActivityId] = fault.ActivityId ?? string.Empty;
                exception.Data[DataKeyDisplayName] = fault.DisplayName ?? string.Empty;
                exception.Data[DataKeyXPath] = fault.XPath ?? string.Empty;
                exception.Data[DataKeyDisplayPath] = fault.DisplayPath ?? string.Empty;
            }
            catch
            {
            }
        }

        public static ActivityInstance ResolveFaultLeaf(ActivityInstance propagatedFrom)
        {
            if (propagatedFrom == null)
            {
                return null;
            }

            ActivityInstance deepest = FindDeepestFaulted(propagatedFrom);
            return deepest ?? propagatedFrom;
        }

        private static Result EmptyResult()
        {
            return new Result
            {
                ActivityId = string.Empty,
                DisplayName = string.Empty,
                XPath = "//",
                DisplayPath = string.Empty
            };
        }

        private static ActivityInstance FindDeepestFaulted(ActivityInstance node)
        {
            if (node == null)
            {
                return null;
            }

            foreach (ActivityInstance child in GetChildInstances(node))
            {
                if (child == null || child.State != ActivityInstanceState.Faulted)
                {
                    continue;
                }

                ActivityInstance deeper = FindDeepestFaulted(child);
                return deeper ?? child;
            }

            return node.State == ActivityInstanceState.Faulted ? node : null;
        }

        private static ActivityInstance FindDeepestDescendant(ActivityInstance node)
        {
            if (node == null)
            {
                return null;
            }

            ActivityInstance deepest = node;
            foreach (ActivityInstance child in GetChildInstances(node))
            {
                if (child == null)
                {
                    continue;
                }

                ActivityInstance candidate = FindDeepestDescendant(child);
                if (candidate != null)
                {
                    deepest = candidate;
                }
            }

            return deepest;
        }

        private static IEnumerable<ActivityInstance> GetChildInstances(ActivityInstance parent)
        {
            if (parent == null)
            {
                yield break;
            }

            object list = null;
            try
            {
                if (ChildListField != null)
                {
                    list = ChildListField.GetValue(parent);
                }

                if (list == null)
                {
                    // Some runtimes use a different field name.
                    foreach (FieldInfo field in typeof(ActivityInstance).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                    {
                        if (field.Name.IndexOf("child", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            continue;
                        }

                        list = field.GetValue(parent);
                        if (list != null)
                        {
                            break;
                        }
                    }
                }
            }
            catch
            {
                yield break;
            }

            if (list == null)
            {
                yield break;
            }

            int count = 0;
            PropertyInfo countProp = list.GetType().GetProperty("Count");
            PropertyInfo itemProp = list.GetType().GetProperty("Item");
            if (countProp == null || itemProp == null)
            {
                yield break;
            }

            try
            {
                count = (int)countProp.GetValue(list, null);
            }
            catch
            {
                yield break;
            }

            for (int i = 0; i < count; i++)
            {
                ActivityInstance child = null;
                try
                {
                    child = itemProp.GetValue(list, new object[] { i }) as ActivityInstance;
                }
                catch
                {
                    continue;
                }

                if (child != null)
                {
                    yield return child;
                }
            }
        }

        private static Activity FindActivityById(Activity root, string activityId)
        {
            if (root == null || string.IsNullOrEmpty(activityId))
            {
                return null;
            }

            var path = new List<Activity>();
            if (TryFindById(root, activityId, path))
            {
                return path[path.Count - 1];
            }

            return null;
        }

        private static List<Activity> FindPath(Activity tryRoot, Activity target)
        {
            if (tryRoot == null || target == null)
            {
                return null;
            }

            var path = new List<Activity>();

            if (!string.IsNullOrEmpty(target.Id))
            {
                if (TryFindById(tryRoot, target.Id, path))
                {
                    return path;
                }

                path.Clear();
            }

            if (TryFindByReference(tryRoot, target, path))
            {
                return path;
            }

            return null;
        }

        private static bool TryFindById(Activity node, string targetId, List<Activity> path)
        {
            if (node == null)
            {
                return false;
            }

            path.Add(node);
            if (string.Equals(node.Id, targetId, StringComparison.Ordinal))
            {
                return true;
            }

            foreach (Activity child in GetChildren(node))
            {
                if (TryFindById(child, targetId, path))
                {
                    return true;
                }
            }

            path.RemoveAt(path.Count - 1);
            return false;
        }

        private static bool TryFindByReference(Activity node, Activity target, List<Activity> path)
        {
            if (node == null)
            {
                return false;
            }

            path.Add(node);
            if (ReferenceEquals(node, target))
            {
                return true;
            }

            foreach (Activity child in GetChildren(node))
            {
                if (TryFindByReference(child, target, path))
                {
                    return true;
                }
            }

            path.RemoveAt(path.Count - 1);
            return false;
        }

        private static string BuildXPath(IList<Activity> chain)
        {
            if (chain == null || chain.Count == 0)
            {
                return "//";
            }

            var sb = new StringBuilder("//");
            for (int i = 0; i < chain.Count; i++)
            {
                Activity node = chain[i];
                string segment = GetTypeSegment(node);

                if (i == 0)
                {
                    sb.Append(segment);
                    continue;
                }

                Activity parent = chain[i - 1];
                int indexAmongType = GetSameTypeIndex(parent, node);
                int sameTypeCount = CountSameTypeChildren(parent, node.GetType());

                sb.Append('/');
                sb.Append(segment);
                if (sameTypeCount > 1 && indexAmongType >= 0)
                {
                    sb.Append('[');
                    sb.Append(indexAmongType);
                    sb.Append(']');
                }
            }

            return sb.ToString();
        }

        private static string BuildDisplayPath(IList<Activity> chain)
        {
            if (chain == null || chain.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            for (int i = 0; i < chain.Count; i++)
            {
                Activity node = chain[i];
                string segment = SanitizePathSegment(GetDisplayLabel(node));

                if (i > 0)
                {
                    sb.Append('/');
                }

                sb.Append(segment);

                if (i == 0)
                {
                    continue;
                }

                Activity parent = chain[i - 1];
                string displayName = GetDisplayLabel(node);
                int indexAmongName = GetSameDisplayNameIndex(parent, node);
                int sameNameCount = CountSameDisplayNameChildren(parent, displayName);

                // Index only among siblings that share the same DisplayName (0-based).
                if (sameNameCount > 1 && indexAmongName >= 0)
                {
                    sb.Append('[');
                    sb.Append(indexAmongName);
                    sb.Append(']');
                }
            }

            return sb.ToString();
        }

        private static int GetSameDisplayNameIndex(Activity parent, Activity child)
        {
            if (parent == null || child == null)
            {
                return -1;
            }

            string childName = GetDisplayLabel(child);
            int index = 0;
            foreach (Activity sibling in GetChildren(parent))
            {
                if (sibling == null)
                {
                    continue;
                }

                if (!string.Equals(GetDisplayLabel(sibling), childName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (ReferenceEquals(sibling, child) ||
                    (!string.IsNullOrEmpty(child.Id) && string.Equals(sibling.Id, child.Id, StringComparison.Ordinal)))
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        private static int CountSameDisplayNameChildren(Activity parent, string displayName)
        {
            if (parent == null)
            {
                return 0;
            }

            int count = 0;
            foreach (Activity sibling in GetChildren(parent))
            {
                if (sibling != null &&
                    string.Equals(GetDisplayLabel(sibling), displayName, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static string SanitizePathSegment(string segment)
        {
            if (string.IsNullOrEmpty(segment))
            {
                return "activity";
            }

            // Keep path separators unambiguous.
            return segment.Replace('/', '_').Replace('\\', '_');
        }

        private static int GetSameTypeIndex(Activity parent, Activity child)
        {
            if (parent == null || child == null)
            {
                return -1;
            }

            Type childType = child.GetType();
            int index = 0;
            foreach (Activity sibling in GetChildren(parent))
            {
                if (sibling == null || sibling.GetType() != childType)
                {
                    continue;
                }

                if (ReferenceEquals(sibling, child) ||
                    (!string.IsNullOrEmpty(child.Id) && string.Equals(sibling.Id, child.Id, StringComparison.Ordinal)))
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        private static int CountSameTypeChildren(Activity parent, Type childType)
        {
            if (parent == null || childType == null)
            {
                return 0;
            }

            int count = 0;
            foreach (Activity sibling in GetChildren(parent))
            {
                if (sibling != null && sibling.GetType() == childType)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Prefer canvas-visible children (Sequence.Activities, While.Body, …) over raw inspection extras.
        /// </summary>
        private static IEnumerable<Activity> GetChildren(Activity parent)
        {
            if (parent == null)
            {
                yield break;
            }

            Collection<Activity> sequenceActivities = TryGetSequenceActivities(parent);
            if (sequenceActivities != null)
            {
                foreach (Activity child in sequenceActivities)
                {
                    if (child != null)
                    {
                        yield return child;
                    }
                }

                yield break;
            }

            Activity whileBody = TryGetPropertyActivity(parent, "Body");
            if (whileBody != null && (parent is While || parent is DoWhile || IsNamed(parent, "ForEach")))
            {
                yield return whileBody;
                yield break;
            }

            if (parent is If)
            {
                var ifActivity = (If)parent;
                if (ifActivity.Then != null)
                {
                    yield return ifActivity.Then;
                }

                if (ifActivity.Else != null)
                {
                    yield return ifActivity.Else;
                }

                yield break;
            }

            if (parent is System.Activities.Statements.Parallel)
            {
                foreach (Activity branch in ((System.Activities.Statements.Parallel)parent).Branches)
                {
                    if (branch != null)
                    {
                        yield return branch;
                    }
                }

                yield break;
            }

            if (parent is System.Activities.Statements.TryCatch)
            {
                var tryCatch = (System.Activities.Statements.TryCatch)parent;
                if (tryCatch.Try != null)
                {
                    yield return tryCatch.Try;
                }

                if (tryCatch.Finally != null)
                {
                    yield return tryCatch.Finally;
                }

                yield break;
            }

            // ActivityAction / delegates: unwrap Handler when present.
            Activity handler = TryGetDelegateHandler(parent);
            if (handler != null)
            {
                yield return handler;
                yield break;
            }

            IEnumerable<Activity> inspected = null;
            try
            {
                inspected = WorkflowInspectionServices.GetActivities(parent);
            }
            catch
            {
                yield break;
            }

            if (inspected == null)
            {
                yield break;
            }

            foreach (Activity child in inspected)
            {
                if (child != null)
                {
                    yield return child;
                }
            }
        }

        private static Collection<Activity> TryGetSequenceActivities(Activity parent)
        {
            var sequence = parent as Sequence;
            if (sequence != null)
            {
                return sequence.Activities;
            }

            // Some hosts use derived sequence types.
            PropertyInfo activitiesProp = parent.GetType().GetProperty("Activities", BindingFlags.Public | BindingFlags.Instance);
            if (activitiesProp != null && typeof(Collection<Activity>).IsAssignableFrom(activitiesProp.PropertyType))
            {
                return activitiesProp.GetValue(parent, null) as Collection<Activity>;
            }

            return null;
        }

        private static Activity TryGetPropertyActivity(Activity parent, string propertyName)
        {
            try
            {
                PropertyInfo prop = parent.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                return prop != null ? prop.GetValue(parent, null) as Activity : null;
            }
            catch
            {
                return null;
            }
        }

        private static Activity TryGetDelegateHandler(Activity parent)
        {
            // Not an Activity — ActivityAction lives as a property on parents; unwrap if parent itself is unusual.
            try
            {
                PropertyInfo handlerProp = parent.GetType().GetProperty("Handler", BindingFlags.Public | BindingFlags.Instance);
                if (handlerProp != null)
                {
                    return handlerProp.GetValue(parent, null) as Activity;
                }
            }
            catch
            {
            }

            return null;
        }

        private static bool IsNamed(Activity activity, string typeNameContains)
        {
            return activity != null &&
                   activity.GetType().Name.IndexOf(typeNameContains, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetTypeSegment(Activity activity)
        {
            if (activity == null)
            {
                return "activity";
            }

            string name = activity.GetType().Name;
            if (name.EndsWith("Activity", StringComparison.Ordinal) && name.Length > "Activity".Length)
            {
                name = name.Substring(0, name.Length - "Activity".Length);
            }

            if (string.Equals(name, "TryCatch", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "TraceableTryCatch", StringComparison.OrdinalIgnoreCase))
            {
                name = "try";
            }

            if (string.Equals(name, "InvokeCode", StringComparison.OrdinalIgnoreCase))
            {
                name = "invokecode";
            }

            return name.ToLowerInvariant();
        }

        private static string GetDisplayLabel(Activity activity)
        {
            if (activity == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(activity.DisplayName))
            {
                return activity.DisplayName.Trim();
            }

            string typeName = activity.GetType().Name;
            if (typeName.EndsWith("Activity", StringComparison.Ordinal) && typeName.Length > "Activity".Length)
            {
                typeName = typeName.Substring(0, typeName.Length - "Activity".Length);
            }

            return typeName;
        }
    }
}
