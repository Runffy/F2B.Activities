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
            /// 1-based index among same-DisplayName siblings ([1] omitted).
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

            /// <summary>Definition-tree chain from Try root to the fault leaf (inclusive).</summary>
            public List<Activity> Chain { get; set; }
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
                DisplayPath = BuildDisplayPath(chain),
                Chain = new List<Activity>(chain)
            };
        }

        public static void EnrichException(
            Exception exception,
            Result fault,
            Activity tryCatchActivity = null,
            string workflowInstanceId = null)
        {
            if (exception == null || fault == null)
            {
                return;
            }

            try
            {
                string composed = ComposeMultiLineFaultSource(
                    exception,
                    fault,
                    tryCatchActivity,
                    workflowInstanceId);
                if (!string.IsNullOrWhiteSpace(composed))
                {
                    exception.Source = composed;
                }
                else if (!string.IsNullOrWhiteSpace(fault.DisplayName))
                {
                    exception.Source = fault.DisplayName;
                }

                exception.Data[DataKeyActivityId] = fault.ActivityId ?? string.Empty;
                exception.Data[DataKeyDisplayName] = fault.DisplayName ?? string.Empty;
                exception.Data[DataKeyXPath] = fault.XPath ?? string.Empty;
                exception.Data[DataKeyDisplayPath] = exception.Source ?? string.Empty;
            }
            catch
            {
            }
        }

        /// <summary>
        /// Multi-line trace: one line per workflow, starting at the root Sequence of the
        /// workflow that hosts Traceable TryCatch.
        /// Example:
        /// [New project/1.xaml] Sequence/Traceable TryCatch/Try/.../Invoke Workflow&lt;New project2/2.xaml&gt;
        /// [New project2/2.xaml] Sequence/.../Invoke OpenRPA&lt;New project/3.xaml&gt;
        /// [New project/3.xaml] Sequence/.../Target
        /// </summary>
        private static string ComposeMultiLineFaultSource(
            Exception exception,
            Result fault,
            Activity tryCatchActivity,
            string workflowInstanceId)
        {
            Activity leaf = fault.Chain != null && fault.Chain.Count > 0
                ? fault.Chain[fault.Chain.Count - 1]
                : null;

            string childSource = FirstNonEmpty(
                exception != null ? exception.Source : null,
                exception != null && exception.InnerException != null ? exception.InnerException.Source : null);

            object homeInstance = TryFindWorkflowInstanceByInstanceId(workflowInstanceId);
            Activity homeRoot = GetWorkflowActivityRoot(homeInstance);

            string homeLabel = FormatWorkflowLabelFromInstance(homeInstance, fallbackKey: null);
            if (string.IsNullOrWhiteSpace(homeLabel))
            {
                homeLabel = "workflow.xaml";
            }

            string homePath = BuildPathFromWorkflowRootToLeaf(homeRoot, tryCatchActivity, leaf, fault);

            var lines = new List<string>();
            bool looksLikeInvoke = leaf != null && IsNestedWorkflowInvokeActivity(leaf);
            bool hasChildPath = !string.IsNullOrWhiteSpace(childSource)
                && !string.Equals(childSource, fault.SourcePath, StringComparison.Ordinal)
                && !string.Equals(childSource, fault.DisplayName, StringComparison.Ordinal)
                && (LooksLikeActivityId(childSource) || childSource.IndexOf('/') >= 0 || childSource.IndexOf('>') >= 0);

            if (looksLikeInvoke && hasChildPath && LooksLikeActivityId(childSource))
            {
                string childKey = FirstNonEmpty(
                    ExtractInvokedWorkflowLookupKey(exception),
                    TryGetWorkflowLookupKeyFromInvokeActivity(leaf));
                object childInstance = TryFindFailedWorkflowInstance(
                    childSource,
                    childKey,
                    workflowInstanceId);
                if (childInstance == null)
                {
                    childInstance = TryFindBestFailedInvokedInstance(fallback: null, childSource);
                }

                string nextLabel = FormatWorkflowLabelFromInstance(childInstance, childKey);
                if (string.IsNullOrWhiteSpace(nextLabel))
                {
                    nextLabel = FormatWorkflowDisplayLabel(null, childKey) ?? "workflow.xaml";
                }

                int invokeIndex = GetInvokeOpenRpaIndex1Based(fault.Chain, leaf);
                nextLabel = FormatIndexedName(nextLabel, invokeIndex);

                lines.Add(FormatTraceLine(homeLabel, homePath, nextLabel));
                AppendInvokedWorkflowTraceLines(
                    exception,
                    childSource,
                    childKey,
                    workflowInstanceId,
                    lines,
                    depth: 0);
            }
            else if (looksLikeInvoke && hasChildPath && !LooksLikeActivityId(childSource))
            {
                // Child source already a breadcrumb (e.g. inner TraceableTryCatch) — keep as single next segment.
                lines.Add(FormatTraceLine(homeLabel, homePath, childSource.Replace("\r\n", " | ").Replace("\n", " | ")));
            }
            else
            {
                lines.Add(FormatTraceLine(homeLabel, homePath, nextWorkflowLabel: null));
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static string FormatTraceLine(string workflowLabel, string pathInside, string nextWorkflowLabel)
        {
            string left = string.IsNullOrWhiteSpace(workflowLabel) ? "workflow.xaml" : workflowLabel.Trim();
            string path = string.IsNullOrWhiteSpace(pathInside) ? "?" : pathInside.Trim();
            if (string.IsNullOrWhiteSpace(nextWorkflowLabel))
            {
                return "[" + left + "] " + path;
            }

            return "[" + left + "] " + path + "<" + nextWorkflowLabel.Trim() + ">";
        }

        private static void AppendInvokedWorkflowTraceLines(
            Exception exception,
            string activityId,
            string workflowKey,
            string parentInstanceId,
            List<string> lines,
            int depth)
        {
            if (string.IsNullOrWhiteSpace(activityId) || depth >= MaxInvokeNestingDepth)
            {
                return;
            }

            if (!LooksLikeActivityId(activityId))
            {
                return;
            }

            object failedInstance = TryFindFailedWorkflowInstance(activityId, workflowKey, parentInstanceId);
            if (failedInstance == null && !string.IsNullOrWhiteSpace(workflowKey))
            {
                failedInstance = TryFindFailedWorkflowInstance(activityId: null, workflowKey, parentInstanceId);
            }

            if (failedInstance == null)
            {
                failedInstance = TryFindBestFailedInvokedInstance(fallback: null, activityId);
            }

            Activity root = failedInstance != null
                ? GetWorkflowActivityRoot(failedInstance)
                : TryLoadWorkflowActivityRoot(workflowKey);
            if (root == null)
            {
                return;
            }

            Activity searchRoot = PreferCanvasRoot(root);
            Activity leaf = FindActivityByIdOrStructural(root, activityId);
            if (leaf == null && !ReferenceEquals(searchRoot, root))
            {
                leaf = FindActivityByIdOrStructural(searchRoot, activityId);
            }

            if (leaf == null)
            {
                return;
            }

            List<Activity> chain = FindPath(searchRoot, leaf) ?? FindPath(root, leaf);
            if (chain == null || chain.Count == 0)
            {
                return;
            }

            bool skipRoot = chain.Count > 1 && IsDynamicActivity(chain[0]);
            string path = BuildDisplayPath(chain, skipRoot);
            if (string.IsNullOrWhiteSpace(path))
            {
                path = GetDisplayLabel(leaf);
            }

            string label = FormatWorkflowLabelFromInstance(failedInstance, workflowKey);
            if (string.IsNullOrWhiteSpace(label))
            {
                label = FormatWorkflowDisplayLabel(null, workflowKey) ?? "workflow.xaml";
            }

            if (!IsNestedWorkflowInvokeActivity(leaf))
            {
                lines.Add(FormatTraceLine(label, path, nextWorkflowLabel: null));
                return;
            }

            string thisInstanceId = failedInstance != null
                ? GetStringProperty(failedInstance, failedInstance.GetType(), "InstanceId")
                : null;

            object childInstance = !string.IsNullOrWhiteSpace(thisInstanceId)
                ? TryFindFailedChildWorkflowInstance(thisInstanceId)
                : null;

            Exception inner = exception != null ? exception.InnerException : null;
            string childSource = FirstNonEmpty(
                childInstance != null
                    ? GetStringProperty(childInstance, childInstance.GetType(), "errorsource", "ErrorSource")
                    : null,
                inner != null ? inner.Source : null);

            if (string.IsNullOrWhiteSpace(childSource))
            {
                lines.Add(FormatTraceLine(label, path, nextWorkflowLabel: null));
                return;
            }

            string childKey = FirstNonEmpty(
                TryGetWorkflowLookupKeyFromInstance(childInstance),
                TryGetWorkflowLookupKeyFromInvokeActivity(leaf),
                ExtractInvokedWorkflowLookupKey(inner),
                ExtractNestedFailedWorkflowKey(exception != null ? exception.Message : null));

            string nextLabel = FormatWorkflowLabelFromInstance(childInstance, childKey);
            if (string.IsNullOrWhiteSpace(nextLabel))
            {
                nextLabel = FormatWorkflowDisplayLabel(null, childKey) ?? "workflow.xaml";
            }

            int invokeIndex = GetInvokeOpenRpaIndex1Based(chain, leaf);
            nextLabel = FormatIndexedName(nextLabel, invokeIndex);

            lines.Add(FormatTraceLine(label, path, nextLabel));

            if (LooksLikeActivityId(childSource))
            {
                AppendInvokedWorkflowTraceLines(
                    inner ?? exception,
                    childSource,
                    childKey,
                    thisInstanceId,
                    lines,
                    depth + 1);
            }
        }

        private static string BuildPathFromWorkflowRootToLeaf(
            Activity workflowRoot,
            Activity tryCatchActivity,
            Activity leaf,
            Result fault)
        {
            Activity searchRoot = PreferCanvasRoot(workflowRoot);
            if (searchRoot != null && leaf != null)
            {
                List<Activity> chain = FindPath(searchRoot, leaf);
                if (chain == null && !string.IsNullOrEmpty(leaf.Id))
                {
                    Activity found = FindActivityByIdOrStructural(searchRoot, leaf.Id)
                                     ?? FindActivityByIdOrStructural(workflowRoot, leaf.Id);
                    if (found != null)
                    {
                        chain = FindPath(searchRoot, found) ?? FindPath(workflowRoot, found);
                    }
                }

                if (chain != null && chain.Count > 0)
                {
                    bool skipRoot = chain.Count > 1 && IsDynamicActivity(chain[0]);
                    string path = BuildDisplayPath(chain, skipRoot);
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        return path;
                    }
                }
            }

            return BuildFallbackHomePath(tryCatchActivity, fault);
        }

        private static string BuildFallbackHomePath(Activity tryCatchActivity, Result fault)
        {
            var sb = new StringBuilder();
            sb.Append("Sequence");
            if (tryCatchActivity != null)
            {
                sb.Append('/');
                sb.Append(SanitizePathSegment(GetDisplayLabel(tryCatchActivity)));
            }

            string local = fault != null ? fault.SourcePath : null;
            if (!string.IsNullOrWhiteSpace(local))
            {
                sb.Append('/');
                sb.Append(local.Trim().TrimStart('/'));
            }

            return sb.ToString();
        }

        private static Activity PreferCanvasRoot(Activity root)
        {
            if (root == null)
            {
                return null;
            }

            if (IsDynamicActivity(root))
            {
                Activity inner = UnwrapDynamicActivity(root);
                return inner ?? root;
            }

            return root;
        }

        private static Activity GetWorkflowActivityRoot(object workflowInstance)
        {
            if (workflowInstance == null)
            {
                return null;
            }

            return InvokeActivityMethod(
                GetPropertyValue(workflowInstance, workflowInstance.GetType(), "Workflow"));
        }

        private static string FormatWorkflowLabelFromInstance(object instance, string fallbackKey)
        {
            string project = TryGetProjectNameFromInstance(instance);
            string key = FirstNonEmpty(
                TryGetWorkflowLookupKeyFromInstance(instance),
                TryGetWorkflowFileNameFromInstance(instance),
                fallbackKey);
            return FormatWorkflowDisplayLabel(project, key);
        }

        /// <summary>
        /// Line header label always prefers project/name.xaml when project is known.
        /// </summary>
        private static string FormatWorkflowDisplayLabel(string workflowProject, string nameOrPath)
        {
            string project;
            string file;
            TrySplitProjectAndWorkflow(nameOrPath, out project, out file);

            if (!string.IsNullOrWhiteSpace(workflowProject))
            {
                project = workflowProject.Trim();
            }

            file = FormatWorkflowFileName(FirstNonEmpty(file, nameOrPath));
            if (string.IsNullOrWhiteSpace(file))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(project))
            {
                return file;
            }

            return project + "/" + file;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return null;
            }

            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return null;
        }

        /// <summary>
        /// OpenRPA sets Exception.Source / errorsource to Activity.Id (e.g. "1.2").
        /// Map that Id to a DisplayName breadcrumb, recursively following nested Invoke OpenRPA /
        /// Invoke Workflow so only the outermost TraceableTryCatch is required.
        /// </summary>
        private static string ResolveInvokedWorkflowSourcePath(
            Exception exception,
            string childSource,
            string homeProjectName)
        {
            return ResolveNestedWorkflowSourcePath(
                exception,
                childSource,
                preferredWorkflowKey: ExtractInvokedWorkflowLookupKey(exception),
                parentInstanceId: null,
                homeProjectName: homeProjectName,
                depth: 0);
        }

        private const int MaxInvokeNestingDepth = 32;

        /// <summary>
        /// Resolve one workflow layer; if the fault leaf is Invoke OpenRPA / Invoke Workflow, append
        /// " &gt; child.xaml: ..." using the child WorkflowInstance (caller chain) and/or InnerException.
        /// </summary>
        private static string ResolveNestedWorkflowSourcePath(
            Exception exception,
            string source,
            string preferredWorkflowKey,
            string parentInstanceId,
            string homeProjectName,
            int depth)
        {
            if (string.IsNullOrWhiteSpace(source) || depth >= MaxInvokeNestingDepth)
            {
                return source;
            }

            // Already a breadcrumb / composed path — keep it (e.g. inner TraceableTryCatch already ran).
            if (!LooksLikeActivityId(source))
            {
                return source;
            }

            try
            {
                object failedInstance = TryFindFailedWorkflowInstance(source, preferredWorkflowKey, parentInstanceId);
                if (failedInstance == null && !string.IsNullOrWhiteSpace(preferredWorkflowKey))
                {
                    // errorsource may already be cleared; still use the failed instance of this workflow.
                    failedInstance = TryFindFailedWorkflowInstance(
                        activityId: null,
                        preferredWorkflowKey,
                        parentInstanceId);
                }

                Activity root = failedInstance != null
                    ? InvokeActivityMethod(GetPropertyValue(failedInstance, failedInstance.GetType(), "Workflow"))
                    : null;
                if (root == null)
                {
                    root = TryLoadWorkflowActivityRoot(preferredWorkflowKey);
                }

                if (root == null)
                {
                    return source;
                }

                // Do NOT call DynamicActivity.Implementation Func — that builds a fresh tree without runtime Ids.
                // Keep the cached root; resolve by Id and/or WF structural Id ("1.2.3").
                Activity leaf = FindActivityByIdOrStructural(root, source);
                if (leaf == null)
                {
                    Activity unwrapped = UnwrapDynamicActivity(root);
                    if (!ReferenceEquals(unwrapped, root))
                    {
                        leaf = FindActivityByIdOrStructural(unwrapped, source);
                        if (leaf != null)
                        {
                            root = unwrapped;
                        }
                    }
                }

                if (leaf == null)
                {
                    return source;
                }

                List<Activity> chain = FindPath(root, leaf);
                if (chain == null || chain.Count == 0)
                {
                    return source;
                }

                // Skip only DynamicActivity wrapper; keep the workflow Sequence in the path.
                bool skipRoot = chain.Count > 1 && IsDynamicActivity(chain[0]);
                string path = BuildDisplayPath(chain, skipRoot);
                if (string.IsNullOrWhiteSpace(path))
                {
                    path = source;
                }

                if (!IsNestedWorkflowInvokeActivity(leaf))
                {
                    return path;
                }

                string thisInstanceId = failedInstance != null
                    ? GetStringProperty(failedInstance, failedInstance.GetType(), "InstanceId")
                    : null;

                object childInstance = !string.IsNullOrWhiteSpace(thisInstanceId)
                    ? TryFindFailedChildWorkflowInstance(thisInstanceId)
                    : null;

                Exception inner = exception != null ? exception.InnerException : null;
                string childSource = FirstNonEmpty(
                    childInstance != null
                        ? GetStringProperty(childInstance, childInstance.GetType(), "errorsource", "ErrorSource")
                        : null,
                    inner != null ? inner.Source : null);

                if (string.IsNullOrWhiteSpace(childSource))
                {
                    return path;
                }

                string childWorkflowKey = FirstNonEmpty(
                    TryGetWorkflowLookupKeyFromInstance(childInstance),
                    TryGetWorkflowLookupKeyFromInvokeActivity(leaf),
                    ExtractInvokedWorkflowLookupKey(inner),
                    ExtractNestedFailedWorkflowKey(exception != null ? exception.Message : null));

                string childXaml = FormatWorkflowDisplaySegment(
                    TryGetProjectNameFromInstance(childInstance),
                    FirstNonEmpty(childWorkflowKey, ExtractInvokedWorkflowLookupKey(inner)),
                    homeProjectName);

                string nestedPath = ResolveNestedWorkflowSourcePath(
                    inner ?? exception,
                    childSource,
                    childWorkflowKey,
                    thisInstanceId,
                    homeProjectName,
                    depth + 1);

                int invokeIndex = GetInvokeOpenRpaIndex1Based(chain, leaf);
                string xamlSegment = FormatIndexedName(childXaml, invokeIndex);
                return path + " > " + xamlSegment + ": " + nestedPath;
            }
            catch
            {
                return source;
            }
        }

        private static bool LooksLikeActivityId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            // WF activity ids: "1", "1.2", "1.2.3", ...
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!(char.IsDigit(c) || c == '.'))
                {
                    return false;
                }
            }

            return value.IndexOf('.') >= 0 || value.Length > 0;
        }

        private static Activity UnwrapDynamicActivity(Activity root)
        {
            if (root == null)
            {
                return null;
            }

            // Prefer WorkflowInspectionServices (cached implementation). Never invoke Implementation Func —
            // that materializes a new tree whose Activity.Id values will not match errorsource.
            try
            {
                Type type = root.GetType();
                if (type.Name.IndexOf("DynamicActivity", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return root;
                }

                foreach (Activity child in WorkflowInspectionServices.GetActivities(root))
                {
                    if (child != null)
                    {
                        return child;
                    }
                }

                PropertyInfo implementationProp = type.GetProperty(
                    "Implementation",
                    BindingFlags.Public | BindingFlags.Instance);
                if (implementationProp != null)
                {
                    object implementation = implementationProp.GetValue(root, null);
                    var asActivity = implementation as Activity;
                    if (asActivity != null)
                    {
                        return asActivity;
                    }
                }
            }
            catch
            {
            }

            return root;
        }

        private static Type FindOpenRpaType(string typeFullName)
        {
            if (string.IsNullOrWhiteSpace(typeFullName))
            {
                return null;
            }

            try
            {
                Type direct = Type.GetType(typeFullName + ", OpenRPA", false);
                if (direct != null)
                {
                    return direct;
                }
            }
            catch
            {
            }

            try
            {
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (Assembly assembly in assemblies)
                {
                    if (assembly == null)
                    {
                        continue;
                    }

                    string name = assembly.GetName().Name;
                    if (!string.Equals(name, "OpenRPA", StringComparison.OrdinalIgnoreCase)
                        && (name == null || name.IndexOf("OpenRPA", StringComparison.OrdinalIgnoreCase) < 0))
                    {
                        continue;
                    }

                    Type type = assembly.GetType(typeFullName, false);
                    if (type != null)
                    {
                        return type;
                    }
                }

                // Last resort: type may live in OpenRPA.Interfaces / host without matching assembly name filter.
                foreach (Assembly assembly in assemblies)
                {
                    if (assembly == null)
                    {
                        continue;
                    }

                    Type type = assembly.GetType(typeFullName, false);
                    if (type != null)
                    {
                        return type;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static Activity TryGetInvokedWorkflowRoot(Exception exception, string activityId)
        {
            object instance = TryFindFailedWorkflowInstance(
                activityId,
                ExtractInvokedWorkflowLookupKey(exception),
                callerInstanceId: null);
            if (instance != null)
            {
                Activity fromInstance = InvokeActivityMethod(
                    GetPropertyValue(instance, instance.GetType(), "Workflow"));
                if (fromInstance != null)
                {
                    return fromInstance;
                }
            }

            string workflowKey = ExtractInvokedWorkflowLookupKey(exception);
            if (string.IsNullOrWhiteSpace(workflowKey))
            {
                return null;
            }

            return TryLoadWorkflowActivityRoot(workflowKey);
        }

        /// <summary>
        /// Locate the failed WorkflowInstance for this activity Id, optionally scoped by
        /// invoked workflow key and/or parent caller InstanceId (disambiguates nested id collisions).
        /// </summary>
        private static object TryFindFailedWorkflowInstance(
            string activityId,
            string workflowKey,
            string callerInstanceId)
        {
            try
            {
                System.Collections.IEnumerable instances = GetOpenRpaWorkflowInstances();
                if (instances == null)
                {
                    return null;
                }

                object best = null;
                int bestScore = int.MinValue;
                foreach (object item in instances)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    Type itemType = item.GetType();
                    string errorsource = GetStringProperty(item, itemType, "errorsource", "ErrorSource");
                    if (!string.IsNullOrWhiteSpace(activityId)
                        && !string.Equals(errorsource, activityId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(callerInstanceId))
                    {
                        string caller = GetStringProperty(item, itemType, "caller", "Caller");
                        if (!string.Equals(caller, callerInstanceId, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(workflowKey)
                        && !WorkflowInstanceMatchesKey(item, workflowKey))
                    {
                        continue;
                    }

                    bool? hasError = GetBoolProperty(item, itemType, "hasError", "HasError");
                    int ident = GetIntProperty(item, itemType, "ident", "Ident") ?? 0;
                    int score = (hasError == true ? 1000 : 0) + ident;
                    if (score >= bestScore)
                    {
                        bestScore = score;
                        best = item;
                    }
                }

                // Fallback: activity id only (no workflow key filter) when key was too strict.
                if (best == null && !string.IsNullOrWhiteSpace(workflowKey) && string.IsNullOrWhiteSpace(callerInstanceId))
                {
                    return TryFindFailedWorkflowInstance(activityId, workflowKey: null, callerInstanceId: null);
                }

                return best;
            }
            catch
            {
                return null;
            }
        }

        private static object TryFindFailedChildWorkflowInstance(string parentInstanceId)
        {
            if (string.IsNullOrWhiteSpace(parentInstanceId))
            {
                return null;
            }

            try
            {
                System.Collections.IEnumerable instances = GetOpenRpaWorkflowInstances();
                if (instances == null)
                {
                    return null;
                }

                object best = null;
                int bestScore = int.MinValue;
                foreach (object item in instances)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    Type itemType = item.GetType();
                    string caller = GetStringProperty(item, itemType, "caller", "Caller");
                    if (!string.Equals(caller, parentInstanceId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    bool? hasError = GetBoolProperty(item, itemType, "hasError", "HasError");
                    string errorsource = GetStringProperty(item, itemType, "errorsource", "ErrorSource");
                    if (hasError != true && string.IsNullOrWhiteSpace(errorsource))
                    {
                        continue;
                    }

                    int ident = GetIntProperty(item, itemType, "ident", "Ident") ?? 0;
                    int score = (hasError == true ? 1000 : 0) + ident;
                    if (score >= bestScore)
                    {
                        bestScore = score;
                        best = item;
                    }
                }

                return best;
            }
            catch
            {
                return null;
            }
        }

        private static System.Collections.IEnumerable GetOpenRpaWorkflowInstances()
        {
            Type wfType = FindOpenRpaType("OpenRPA.WorkflowInstance");
            PropertyInfo instancesProp = wfType != null
                ? wfType.GetProperty("Instances", BindingFlags.Public | BindingFlags.Static)
                : null;
            return instancesProp != null
                ? instancesProp.GetValue(null, null) as System.Collections.IEnumerable
                : null;
        }

        private static bool WorkflowInstanceMatchesKey(object instance, string workflowKey)
        {
            if (instance == null || string.IsNullOrWhiteSpace(workflowKey))
            {
                return false;
            }

            string key = workflowKey.Trim();
            string fromInstance = TryGetWorkflowLookupKeyFromInstance(instance);
            if (!string.IsNullOrWhiteSpace(fromInstance)
                && (string.Equals(fromInstance, key, StringComparison.OrdinalIgnoreCase)
                    || EndsWithWorkflowName(fromInstance, key)
                    || EndsWithWorkflowName(key, fromInstance)))
            {
                return true;
            }

            object workflow = GetPropertyValue(instance, instance.GetType(), "Workflow");
            if (workflow == null)
            {
                return false;
            }

            Type wfType = workflow.GetType();
            string[] candidates =
            {
                GetStringProperty(workflow, wfType, "ProjectAndName"),
                GetStringProperty(workflow, wfType, "RelativeFilename"),
                GetStringProperty(workflow, wfType, "Filename"),
                GetStringProperty(workflow, wfType, "name", "Name"),
                GetStringProperty(workflow, wfType, "_id", "Id")
            };

            foreach (string candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                if (string.Equals(candidate, key, StringComparison.OrdinalIgnoreCase)
                    || EndsWithWorkflowName(candidate, key)
                    || EndsWithWorkflowName(key, candidate))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool EndsWithWorkflowName(string full, string part)
        {
            if (string.IsNullOrWhiteSpace(full) || string.IsNullOrWhiteSpace(part))
            {
                return false;
            }

            string a = full.Trim().Replace('\\', '/');
            string b = part.Trim().Replace('\\', '/');
            if (a.EndsWith(b, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string aFile = FormatWorkflowFileName(a);
            string bFile = FormatWorkflowFileName(b);
            return !string.IsNullOrWhiteSpace(aFile)
                   && !string.IsNullOrWhiteSpace(bFile)
                   && string.Equals(aFile, bFile, StringComparison.OrdinalIgnoreCase);
        }

        private static string TryGetWorkflowLookupKeyFromInstance(object instance)
        {
            if (instance == null)
            {
                return null;
            }

            object workflow = GetPropertyValue(instance, instance.GetType(), "Workflow");
            if (workflow == null)
            {
                return GetStringProperty(instance, instance.GetType(), "name", "Name", "projectname", "ProjectName");
            }

            Type wfType = workflow.GetType();
            return FirstNonEmpty(
                GetStringProperty(workflow, wfType, "ProjectAndName"),
                GetStringProperty(workflow, wfType, "RelativeFilename"),
                GetStringProperty(workflow, wfType, "Filename"),
                GetStringProperty(workflow, wfType, "name", "Name"));
        }

        private static string TryGetWorkflowLookupKeyFromInvokeActivity(Activity invoke)
        {
            if (invoke == null)
            {
                return null;
            }

            try
            {
                PropertyInfo workflowProp = invoke.GetType().GetProperty(
                    "workflow",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (workflowProp == null)
                {
                    return null;
                }

                object argument = workflowProp.GetValue(invoke, null);
                if (argument == null)
                {
                    return null;
                }

                // InArgument / InArgument<string>: Expression.ExpressionText or Literal.Value
                PropertyInfo expressionProp = argument.GetType().GetProperty(
                    "Expression",
                    BindingFlags.Public | BindingFlags.Instance);
                object expression = expressionProp != null ? expressionProp.GetValue(argument, null) : null;
                if (expression != null)
                {
                    PropertyInfo textProp = expression.GetType().GetProperty(
                        "ExpressionText",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (textProp != null)
                    {
                        string text = textProp.GetValue(expression, null) as string;
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            return UnquoteExpressionText(text);
                        }
                    }

                    PropertyInfo valueProp = expression.GetType().GetProperty(
                        "Value",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (valueProp != null)
                    {
                        object value = valueProp.GetValue(expression, null);
                        if (value != null)
                        {
                            return value.ToString();
                        }
                    }
                }

                return argument.ToString();
            }
            catch
            {
                return null;
            }
        }

        private static string UnquoteExpressionText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            string t = text.Trim();
            if (t.Length >= 2)
            {
                if ((t[0] == '"' && t[t.Length - 1] == '"') || (t[0] == '\'' && t[t.Length - 1] == '\''))
                {
                    return t.Substring(1, t.Length - 2);
                }
            }

            return t;
        }

        /// <summary>
        /// From "A failed with B failed with msg" return "B" (the next nested invoke name).
        /// </summary>
        private static string ExtractNestedFailedWorkflowKey(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return null;
            }

            const string marker = " failed with ";
            int first = message.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (first < 0)
            {
                return null;
            }

            string rest = message.Substring(first + marker.Length);
            int second = rest.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (second <= 0)
            {
                return null;
            }

            return rest.Substring(0, second).Trim();
        }

        private static string FormatWorkflowFileName(string nameOrPath)
        {
            if (string.IsNullOrWhiteSpace(nameOrPath))
            {
                return null;
            }

            string name = nameOrPath.Trim();
            int slash = Math.Max(name.LastIndexOf('/'), name.LastIndexOf('\\'));
            if (slash >= 0 && slash < name.Length - 1)
            {
                name = name.Substring(slash + 1);
            }

            if (!name.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            {
                name = name + ".xaml";
            }

            return name;
        }

        private static int? GetIntProperty(object target, Type type, params string[] names)
        {
            object value = GetPropertyValue(target, type, names);
            if (value == null)
            {
                return null;
            }

            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return null;
            }
        }

        private static Activity TryGetRootFromFailedWorkflowInstance(string activityId)
        {
            object instance = TryFindFailedWorkflowInstance(activityId, workflowKey: null, callerInstanceId: null);
            return instance != null
                ? InvokeActivityMethod(GetPropertyValue(instance, instance.GetType(), "Workflow"))
                : null;
        }

        private static string ExtractInvokedWorkflowLookupKey(Exception exception)
        {
            string message = exception != null ? exception.Message : null;
            if (string.IsNullOrWhiteSpace(message) && exception != null && exception.InnerException != null)
            {
                message = exception.InnerException.Message;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                return null;
            }

            const string marker = " failed with ";
            int index = message.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index <= 0)
            {
                return null;
            }

            return message.Substring(0, index).Trim();
        }

        private static Activity TryLoadWorkflowActivityRoot(string workflowKey)
        {
            try
            {
                Type robotType = FindOpenRpaType("OpenRPA.RobotInstance");
                if (robotType == null)
                {
                    return null;
                }

                PropertyInfo instanceProp = robotType.GetProperty(
                    "instance",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);
                object robot = instanceProp != null ? instanceProp.GetValue(null, null) : null;
                if (robot == null)
                {
                    return null;
                }

                MethodInfo getter = robot.GetType().GetMethod(
                    "GetWorkflowByIDOrRelativeFilename",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { typeof(string) },
                    null);
                if (getter == null)
                {
                    return null;
                }

                object workflow = string.IsNullOrWhiteSpace(workflowKey)
                    ? null
                    : getter.Invoke(robot, new object[] { workflowKey });
                if (workflow == null && !string.IsNullOrWhiteSpace(workflowKey))
                {
                    // Try filename-only fallback: "New project/2" → "2" / "2.xaml"
                    string fileOnly = ExtractInvokedWorkflowFileName(new Exception(workflowKey + " failed with x"));
                    if (!string.Equals(fileOnly, workflowKey, StringComparison.OrdinalIgnoreCase))
                    {
                        workflow = getter.Invoke(robot, new object[] { fileOnly });
                    }

                    if (workflow == null)
                    {
                        string nameOnly = fileOnly;
                        if (nameOnly.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                        {
                            nameOnly = nameOnly.Substring(0, nameOnly.Length - 5);
                        }

                        if (!string.IsNullOrWhiteSpace(nameOnly)
                            && !string.Equals(nameOnly, workflowKey, StringComparison.OrdinalIgnoreCase))
                        {
                            workflow = getter.Invoke(robot, new object[] { nameOnly });
                        }
                    }
                }

                return InvokeActivityMethod(workflow);
            }
            catch
            {
                return null;
            }
        }

        private static Activity InvokeActivityMethod(object workflow)
        {
            if (workflow == null)
            {
                return null;
            }

            try
            {
                MethodInfo activityMethod = workflow.GetType().GetMethod(
                    "Activity",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    Type.EmptyTypes,
                    null);
                if (activityMethod == null)
                {
                    return null;
                }

                return activityMethod.Invoke(workflow, null) as Activity;
            }
            catch
            {
                return null;
            }
        }

        private static string GetStringProperty(object target, Type type, params string[] names)
        {
            object value = GetPropertyValue(target, type, names);
            return value as string;
        }

        private static bool? GetBoolProperty(object target, Type type, params string[] names)
        {
            object value = GetPropertyValue(target, type, names);
            if (value is bool)
            {
                return (bool)value;
            }

            return null;
        }

        private static object GetPropertyValue(object target, Type type, params string[] names)
        {
            if (target == null || type == null || names == null)
            {
                return null;
            }

            foreach (string name in names)
            {
                PropertyInfo property = type.GetProperty(
                    name,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (property == null)
                {
                    continue;
                }

                try
                {
                    return property.GetValue(target, null);
                }
                catch
                {
                }
            }

            return null;
        }


        private static bool IsNestedWorkflowInvokeActivity(Activity activity)
        {
            if (activity == null)
            {
                return false;
            }

            if (activity is InvokeWorkflowActivity)
            {
                return true;
            }

            string typeName = activity.GetType().Name ?? string.Empty;
            return typeName.IndexOf("InvokeOpenRPA", StringComparison.OrdinalIgnoreCase) >= 0
                   || typeName.IndexOf("InvokeOpenRpa", StringComparison.OrdinalIgnoreCase) >= 0
                   || typeName.IndexOf("InvokeWorkflow", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ExtractInvokedWorkflowFileName(Exception exception)
        {
            for (Exception ex = exception; ex != null; ex = ex.InnerException)
            {
                string key = ExtractInvokedWorkflowLookupKey(ex);
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                string fileName = FormatWorkflowFileName(key);
                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    return fileName;
                }
            }

            return null;
        }

        /// <summary>
        /// Display name for an invoked workflow relative to the starting workflow's project:
        /// same project → "2.xaml"; other project → "OtherProject/2.xaml".
        /// </summary>
        private static string ResolveInvokedWorkflowDisplayName(
            Exception exception,
            Activity invokeLeaf,
            string childSource,
            string homeProjectName)
        {
            for (Exception ex = exception; ex != null; ex = ex.InnerException)
            {
                string key = ExtractInvokedWorkflowLookupKey(ex);
                if (!string.IsNullOrWhiteSpace(key))
                {
                    return FormatWorkflowDisplaySegment(null, key, homeProjectName);
                }
            }

            string invokeKey = TryGetWorkflowLookupKeyFromInvokeActivity(invokeLeaf);
            if (!string.IsNullOrWhiteSpace(invokeKey))
            {
                return FormatWorkflowDisplaySegment(null, invokeKey, homeProjectName);
            }

            object failedInstance = TryFindFailedWorkflowInstance(
                childSource,
                workflowKey: null,
                callerInstanceId: null);
            if (failedInstance == null)
            {
                failedInstance = TryFindBestFailedInvokedInstance(fallback: null, childSource);
            }

            string project = TryGetProjectNameFromInstance(failedInstance);
            string nameOrPath = FirstNonEmpty(
                TryGetWorkflowLookupKeyFromInstance(failedInstance),
                TryGetWorkflowFileNameFromInstance(failedInstance));
            if (!string.IsNullOrWhiteSpace(nameOrPath) || !string.IsNullOrWhiteSpace(project))
            {
                return FormatWorkflowDisplaySegment(project, nameOrPath ?? "workflow", homeProjectName);
            }

            return "workflow.xaml";
        }

        /// <summary>
        /// Project of the workflow that started the run (outermost caller), used to shorten same-project paths.
        /// </summary>
        private static string TryResolveHomeProjectName(Exception exception, string childSource)
        {
            try
            {
                object child = TryFindFailedWorkflowInstance(
                    childSource,
                    ExtractInvokedWorkflowLookupKey(exception),
                    callerInstanceId: null);
                if (child == null)
                {
                    child = TryFindBestFailedInvokedInstance(fallback: null, childSource);
                }

                object root = TryFindRootCallerInstance(child);
                string fromRoot = TryGetProjectNameFromInstance(root);
                if (!string.IsNullOrWhiteSpace(fromRoot))
                {
                    return fromRoot;
                }

                return TryGetRootWorkflowProjectName();
            }
            catch
            {
                return null;
            }
        }

        private static object TryFindRootCallerInstance(object startInstance)
        {
            object current = startInstance;
            for (int i = 0; i < MaxInvokeNestingDepth && current != null; i++)
            {
                string callerId = GetStringProperty(current, current.GetType(), "caller", "Caller");
                if (string.IsNullOrWhiteSpace(callerId))
                {
                    return current;
                }

                object parent = TryFindWorkflowInstanceByInstanceId(callerId);
                if (parent == null)
                {
                    return current;
                }

                current = parent;
            }

            return current;
        }

        private static object TryFindWorkflowInstanceByInstanceId(string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                return null;
            }

            try
            {
                System.Collections.IEnumerable instances = GetOpenRpaWorkflowInstances();
                if (instances == null)
                {
                    return null;
                }

                foreach (object item in instances)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    string id = GetStringProperty(item, item.GetType(), "InstanceId");
                    if (string.Equals(id, instanceId, StringComparison.OrdinalIgnoreCase))
                    {
                        return item;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static string TryGetRootWorkflowProjectName()
        {
            try
            {
                System.Collections.IEnumerable instances = GetOpenRpaWorkflowInstances();
                if (instances == null)
                {
                    return null;
                }

                object best = null;
                int bestScore = int.MinValue;
                foreach (object item in instances)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    Type itemType = item.GetType();
                    string caller = GetStringProperty(item, itemType, "caller", "Caller");
                    if (!string.IsNullOrWhiteSpace(caller))
                    {
                        continue;
                    }

                    bool? completed = GetBoolProperty(item, itemType, "isCompleted", "IsCompleted");
                    bool? hasError = GetBoolProperty(item, itemType, "hasError", "HasError");
                    int ident = GetIntProperty(item, itemType, "ident", "Ident") ?? 0;
                    int score = (completed == false ? 1000 : 0) + (hasError == true ? 100 : 0) - ident;
                    if (score >= bestScore)
                    {
                        bestScore = score;
                        best = item;
                    }
                }

                return TryGetProjectNameFromInstance(best);
            }
            catch
            {
                return null;
            }
        }

        private static string TryGetProjectNameFromInstance(object instance)
        {
            if (instance == null)
            {
                return null;
            }

            Type itemType = instance.GetType();
            string fromInstance = GetStringProperty(instance, itemType, "projectname", "ProjectName");

            object workflow = GetPropertyValue(instance, itemType, "Workflow");
            if (workflow != null)
            {
                Type wfType = workflow.GetType();
                string projectAndName = GetStringProperty(workflow, wfType, "ProjectAndName");
                string projectFromKey;
                string unusedFile;
                TrySplitProjectAndWorkflow(projectAndName, out projectFromKey, out unusedFile);

                string fromProjectEntity = null;
                try
                {
                    MethodInfo projectMethod = wfType.GetMethod(
                        "Project",
                        BindingFlags.Public | BindingFlags.Instance,
                        null,
                        Type.EmptyTypes,
                        null);
                    object project = projectMethod != null ? projectMethod.Invoke(workflow, null) : null;
                    if (project != null)
                    {
                        fromProjectEntity = GetStringProperty(project, project.GetType(), "name", "Name");
                    }
                }
                catch
                {
                }

                string relative = GetStringProperty(workflow, wfType, "RelativeFilename");
                string projectFromRelative;
                TrySplitProjectAndWorkflow(relative, out projectFromRelative, out unusedFile);

                return FirstNonEmpty(fromProjectEntity, projectFromKey, projectFromRelative, fromInstance);
            }

            return fromInstance;
        }

        /// <summary>
        /// Same project as the starting workflow → "name.xaml"; otherwise "project/name.xaml".
        /// </summary>
        private static string FormatWorkflowDisplaySegment(
            string workflowProject,
            string nameOrPath,
            string homeProjectName)
        {
            string project;
            string file;
            TrySplitProjectAndWorkflow(nameOrPath, out project, out file);

            if (!string.IsNullOrWhiteSpace(workflowProject))
            {
                project = workflowProject.Trim();
            }

            file = FormatWorkflowFileName(FirstNonEmpty(file, nameOrPath));
            if (string.IsNullOrWhiteSpace(file))
            {
                file = "workflow.xaml";
            }

            if (string.IsNullOrWhiteSpace(project)
                || (!string.IsNullOrWhiteSpace(homeProjectName)
                    && string.Equals(project, homeProjectName, StringComparison.OrdinalIgnoreCase)))
            {
                return file;
            }

            return project + "/" + file;
        }

        private static void TrySplitProjectAndWorkflow(string nameOrPath, out string project, out string file)
        {
            project = null;
            file = null;
            if (string.IsNullOrWhiteSpace(nameOrPath))
            {
                return;
            }

            string value = nameOrPath.Trim().Replace('\\', '/');
            int slash = value.LastIndexOf('/');
            if (slash > 0 && slash < value.Length - 1)
            {
                project = value.Substring(0, slash).Trim();
                file = value.Substring(slash + 1).Trim();
            }
            else
            {
                file = value;
            }
        }

        private static string TryGetWorkflowFileNameFromInstance(object instance)
        {
            if (instance == null)
            {
                return null;
            }

            object workflow = GetPropertyValue(instance, instance.GetType(), "Workflow");
            if (workflow == null)
            {
                return FirstNonEmpty(
                    GetStringProperty(instance, instance.GetType(), "Filename", "filename"),
                    GetStringProperty(instance, instance.GetType(), "RelativeFilename", "relativeFilename"),
                    GetStringProperty(instance, instance.GetType(), "name", "Name"));
            }

            Type wfType = workflow.GetType();
            return FirstNonEmpty(
                GetStringProperty(workflow, wfType, "Filename"),
                GetStringProperty(workflow, wfType, "RelativeFilename"),
                GetStringProperty(workflow, wfType, "name", "Name"),
                GetStringProperty(workflow, wfType, "ProjectAndName"));
        }

        private static object TryFindBestFailedInvokedInstance(object fallback, string childSource)
        {
            try
            {
                System.Collections.IEnumerable instances = GetOpenRpaWorkflowInstances();
                if (instances == null)
                {
                    return fallback;
                }

                object best = null;
                int bestScore = int.MinValue;
                foreach (object item in instances)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    Type itemType = item.GetType();
                    bool? hasError = GetBoolProperty(item, itemType, "hasError", "HasError");
                    string errorsource = GetStringProperty(item, itemType, "errorsource", "ErrorSource");
                    string caller = GetStringProperty(item, itemType, "caller", "Caller");
                    if (hasError != true && string.IsNullOrWhiteSpace(errorsource))
                    {
                        continue;
                    }

                    // Invoked workflows have a caller; the outermost run usually does not.
                    if (string.IsNullOrWhiteSpace(caller))
                    {
                        continue;
                    }

                    int ident = GetIntProperty(item, itemType, "ident", "Ident") ?? 0;
                    int score = (hasError == true ? 1000 : 0) + ident;
                    if (!string.IsNullOrWhiteSpace(childSource)
                        && string.Equals(errorsource, childSource, StringComparison.Ordinal))
                    {
                        score += 5000;
                    }

                    if (score >= bestScore)
                    {
                        bestScore = score;
                        best = item;
                    }
                }

                return best ?? fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static int GetInvokeOpenRpaIndex1Based(IList<Activity> chain, Activity invokeLeaf)
        {
            if (chain == null || chain.Count < 2 || invokeLeaf == null)
            {
                return 1;
            }

            Activity parent = chain[chain.Count - 2];
            int index = 0;
            foreach (Activity sibling in GetChildren(parent))
            {
                if (!IsNestedWorkflowInvokeActivity(sibling))
                {
                    continue;
                }

                index++;
                if (ReferenceEquals(sibling, invokeLeaf) ||
                    (!string.IsNullOrEmpty(invokeLeaf.Id)
                     && string.Equals(sibling.Id, invokeLeaf.Id, StringComparison.Ordinal)))
                {
                    return index;
                }
            }

            return 1;
        }

        private static string FormatIndexedName(string name, int index1Based)
        {
            if (index1Based > 1)
            {
                return name + "[" + index1Based + "]";
            }

            return name;
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

        private static Activity FindActivityByIdOrStructural(Activity root, string activityId)
        {
            Activity byId = FindActivityById(root, activityId);
            if (byId != null)
            {
                return byId;
            }

            return FindActivityByStructuralId(root, activityId);
        }

        private static bool IsDynamicActivity(Activity activity)
        {
            return activity != null
                   && activity.GetType().Name.IndexOf("DynamicActivity", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsActivityDelegateWrapper(Activity activity)
        {
            if (activity == null)
            {
                return false;
            }

            string name = activity.GetType().Name ?? string.Empty;
            return name.StartsWith("ActivityAction", StringComparison.Ordinal)
                   || name.StartsWith("ActivityFunc", StringComparison.Ordinal);
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

            // Retry with WF inspection children — matches runtime Id assignment more closely.
            path.Clear();
            if (TryFindByIdInspection(root, activityId, path))
            {
                return path[path.Count - 1];
            }

            return null;
        }

        /// <summary>
        /// Resolve WF Activity.Id structurally: "1.2.3" = root → 2nd child → 3rd child
        /// (1-based sibling indices via WorkflowInspectionServices).
        /// Works even when a freshly loaded tree has not yet assigned Id properties.
        /// </summary>
        private static Activity FindActivityByStructuralId(Activity root, string activityId)
        {
            if (root == null || string.IsNullOrWhiteSpace(activityId) || !LooksLikeActivityId(activityId))
            {
                return null;
            }

            string[] parts = activityId.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < parts.Length; i++)
            {
                int segment;
                if (!int.TryParse(parts[i], out segment) || segment < 1)
                {
                    return null;
                }
            }

            Activity current = root;

            // parts[0] is the root segment (normally "1"). Remaining parts are 1-based child indices.
            for (int i = 1; i < parts.Length; i++)
            {
                int index1Based = int.Parse(parts[i]);
                List<Activity> children = GetInspectionChildrenList(current);
                if (children.Count == 0)
                {
                    // Fall back to display children (Sequence.Activities / While Body handler, etc.)
                    children = new List<Activity>();
                    foreach (Activity child in GetChildren(current))
                    {
                        if (child != null)
                        {
                            children.Add(child);
                        }
                    }
                }

                if (index1Based > children.Count)
                {
                    return null;
                }

                current = children[index1Based - 1];
                if (current == null)
                {
                    return null;
                }
            }

            return current;
        }

        private static bool TryFindByIdInspection(Activity node, string targetId, List<Activity> path)
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

            foreach (Activity child in GetInspectionChildrenList(node))
            {
                if (TryFindByIdInspection(child, targetId, path))
                {
                    return true;
                }
            }

            path.RemoveAt(path.Count - 1);
            return false;
        }

        private static List<Activity> GetInspectionChildrenList(Activity parent)
        {
            var result = new List<Activity>();
            if (parent == null)
            {
                return result;
            }

            try
            {
                foreach (Activity child in WorkflowInspectionServices.GetActivities(parent))
                {
                    if (child != null)
                    {
                        result.Add(child);
                    }
                }
            }
            catch
            {
            }

            return result;
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
                // 1-based among same-type siblings; omit [1].
                if (sameTypeCount > 1 && indexAmongType >= 0)
                {
                    int index1Based = indexAmongType + 1;
                    if (index1Based > 1)
                    {
                        sb.Append('[');
                        sb.Append(index1Based);
                        sb.Append(']');
                    }
                }
            }

            return sb.ToString();
        }

        private static string BuildDisplayPath(IList<Activity> chain)
        {
            return BuildDisplayPath(chain, skipRoot: false);
        }

        private static string BuildDisplayPath(IList<Activity> chain, bool skipRoot)
        {
            if (chain == null || chain.Count == 0)
            {
                return string.Empty;
            }

            int startIndex = skipRoot && chain.Count > 1 ? 1 : 0;
            var sb = new StringBuilder();
            for (int i = startIndex; i < chain.Count; i++)
            {
                Activity node = chain[i];
                if (IsActivityDelegateWrapper(node))
                {
                    continue;
                }

                string segment = SanitizePathSegment(GetDisplayLabel(node));
                if (string.IsNullOrWhiteSpace(segment))
                {
                    continue;
                }

                if (sb.Length > 0)
                {
                    sb.Append('/');
                }

                sb.Append(segment);

                // Sibling index: use nearest non-wrapper ancestor in the chain.
                Activity parent = null;
                for (int p = i - 1; p >= 0; p--)
                {
                    if (!IsActivityDelegateWrapper(chain[p]))
                    {
                        parent = chain[p];
                        break;
                    }
                }

                if (parent == null)
                {
                    continue;
                }

                string displayName = GetDisplayLabel(node);
                int indexAmongName = GetSameDisplayNameIndex(parent, node);
                int sameNameCount = CountSameDisplayNameChildren(parent, displayName);

                // 1-based among same-DisplayName siblings; omit [1].
                if (sameNameCount > 1 && indexAmongName >= 0)
                {
                    int index1Based = indexAmongName + 1;
                    if (index1Based > 1)
                    {
                        sb.Append('[');
                        sb.Append(index1Based);
                        sb.Append(']');
                    }
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

            // Traceable TryCatch: walk into Try (and Finally) so root→Invoke paths include this scope.
            if (IsNamed(parent, "TraceableTryCatch"))
            {
                Activity tryBody = TryGetPropertyActivity(parent, "Try");
                if (tryBody != null)
                {
                    yield return tryBody;
                }

                Activity finallyBody = TryGetPropertyActivity(parent, "Finally");
                if (finallyBody != null)
                {
                    yield return finallyBody;
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
