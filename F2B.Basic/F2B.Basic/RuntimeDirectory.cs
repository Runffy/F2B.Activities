using OpenRPA.Interfaces;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;

namespace F2B.Basic
{
    /// <summary>
    /// Timestamp precision used when creating the runtime folder name.
    /// </summary>
    public enum RuntimeDirectoryMode
    {
        Year,
        Month,
        Day,
        Hour,
        Minute,
        Second
    }

    /// <summary>
    /// Per-run runtime directory under OpenRPA ProjectsDirectory\Runtime\{projectname}\{timestamp}.
    /// Resource counterpart: ProjectsDirectory\Projects\{projectname} (<see cref="ResourceDirectory"/>).
    /// Project name and folder identity always follow the outermost source workflow
    /// (walk OpenRPA WorkflowInstance.caller), so nested Invoke OpenRPA into another project
    /// still resolves under the original project's Runtime folder.
    /// Usage: <c>F2B.Basic.RuntimeDirectory.Path</c> (always Second precision).
    /// Second mode shares its stamp with LogMessage when either runs first.
    /// </summary>
    public static class RuntimeDirectory
    {
        private const int MaxCallerDepth = 32;

        private static readonly ConcurrentDictionary<string, string> PathsByWorkflowInstanceId =
            new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Runtime directory for the current OpenRPA workflow run (timestamp precision: Second).
        /// Create on first access; reuse for later calls in the same run (including nested invokes).
        /// </summary>
        public static string Path
        {
            get
            {
                string instanceId;
                string projectName;
                TryResolveCurrentWorkflow(out instanceId, out projectName);
                return GetOrCreate(instanceId, projectName, RuntimeDirectoryMode.Second);
            }
        }

        public static bool TryGetExistingPath(string workflowInstanceId, out string runtimeDirectory)
        {
            string key = ResolveRootInstanceKey(workflowInstanceId);
            return PathsByWorkflowInstanceId.TryGetValue(key, out runtimeDirectory)
                   && !string.IsNullOrWhiteSpace(runtimeDirectory);
        }

        /// <summary>
        /// Returns the runtime directory for the given workflow instance, creating it on first use.
        /// Nested instances are remapped to the outermost caller before creating/looking up the path.
        /// </summary>
        public static string GetOrCreate(
            string workflowInstanceId,
            string projectName,
            RuntimeDirectoryMode mode = RuntimeDirectoryMode.Second)
        {
            string rootInstanceId;
            string rootProjectName;
            ResolveRootWorkflow(workflowInstanceId, projectName, out rootInstanceId, out rootProjectName);

            string key = string.IsNullOrWhiteSpace(rootInstanceId)
                ? string.Empty
                : rootInstanceId.Trim();

            return PathsByWorkflowInstanceId.GetOrAdd(key, _ => CreateDirectory(key, rootProjectName, mode));
        }

        /// <summary>
        /// Same as <see cref="Path"/>, but prefers the workflow instance bound to <paramref name="context"/>.
        /// </summary>
        public static string GetOrCreate(
            System.Activities.CodeActivityContext context,
            RuntimeDirectoryMode mode = RuntimeDirectoryMode.Second)
        {
            if (context == null)
            {
                string instanceId;
                string projectName;
                TryResolveCurrentWorkflow(out instanceId, out projectName);
                return GetOrCreate(instanceId, projectName, mode);
            }

            string workflowInstanceId = context.WorkflowInstanceId.ToString();
            string projectNameFromContext = ResolveProjectName(workflowInstanceId);
            return GetOrCreate(workflowInstanceId, projectNameFromContext, mode);
        }

        /// <summary>
        /// Outermost source workflow project name (Invoke OpenRPA caller chain). Used by ResourceDirectory too.
        /// </summary>
        public static string ResolveSourceProjectName(System.Activities.CodeActivityContext context)
        {
            if (context == null)
            {
                string instanceId;
                string projectName;
                TryResolveCurrentWorkflow(out instanceId, out projectName);
                return string.IsNullOrWhiteSpace(projectName) ? null : projectName.Trim();
            }

            string rootInstanceId;
            string rootProjectName;
            ResolveRootWorkflow(
                context.WorkflowInstanceId.ToString(),
                projectNameHint: null,
                out rootInstanceId,
                out rootProjectName);
            return string.IsNullOrWhiteSpace(rootProjectName) ? null : rootProjectName.Trim();
        }

        /// <summary>
        /// Outermost source workflow project name for the current OpenRPA run.
        /// </summary>
        public static string ResolveSourceProjectName()
        {
            string instanceId;
            string projectName;
            TryResolveCurrentWorkflow(out instanceId, out projectName);
            return string.IsNullOrWhiteSpace(projectName) ? null : projectName.Trim();
        }

        /// <summary>
        /// Outermost source workflow InstanceId for the workflow bound to <paramref name="context"/>.
        /// </summary>
        public static string ResolveSourceInstanceId(System.Activities.CodeActivityContext context)
        {
            if (context == null)
            {
                return ResolveSourceInstanceId();
            }

            string rootInstanceId;
            string unusedProject;
            ResolveRootWorkflow(
                context.WorkflowInstanceId.ToString(),
                projectNameHint: null,
                out rootInstanceId,
                out unusedProject);
            return string.IsNullOrWhiteSpace(rootInstanceId) ? null : rootInstanceId.Trim();
        }

        /// <summary>
        /// Outermost source workflow InstanceId for the current OpenRPA run.
        /// </summary>
        public static string ResolveSourceInstanceId()
        {
            string instanceId;
            string projectName;
            TryResolveCurrentWorkflow(out instanceId, out projectName);
            return string.IsNullOrWhiteSpace(instanceId) ? null : instanceId.Trim();
        }

        /// <summary>
        /// True when the given source InstanceId is still an active (not completed) OpenRPA run.
        /// </summary>
        internal static bool IsSourceInstanceActive(string sourceInstanceId)
        {
            if (string.IsNullOrWhiteSpace(sourceInstanceId))
            {
                return false;
            }

            object instance = FindWorkflowInstanceById(sourceInstanceId);
            if (instance == null)
            {
                return false;
            }

            bool? completed = GetBoolPropertyValue(instance, "isCompleted");
            return completed != true;
        }

        /// <summary>
        /// Enumerate outermost InstanceIds that are still active.
        /// </summary>
        internal static System.Collections.Generic.HashSet<string> GetActiveSourceInstanceIds()
        {
            var active = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var instances = GetWorkflowInstances();
            if (instances == null)
            {
                return active;
            }

            foreach (object item in instances)
            {
                if (item == null)
                {
                    continue;
                }

                bool? completed = GetBoolPropertyValue(item, "isCompleted");
                if (completed == true)
                {
                    continue;
                }

                string localId = GetStringPropertyValue(item, "InstanceId");
                string rootKey = ResolveRootInstanceKey(localId);
                if (!string.IsNullOrWhiteSpace(rootKey))
                {
                    active.Add(rootKey);
                }
            }

            return active;
        }

        private static string CreateDirectory(string workflowInstanceId, string projectName, RuntimeDirectoryMode mode)
        {
            string safeProjectName = SanitizePathSegment(
                string.IsNullOrWhiteSpace(projectName) ? "UnknownProject" : projectName.Trim());
            string stamp = ResolveStamp(workflowInstanceId, mode);
            string directory = System.IO.Path.Combine(
                Extensions.ProjectsDirectory,
                "Runtime",
                safeProjectName,
                stamp);

            Directory.CreateDirectory(directory);

            if (mode == RuntimeDirectoryMode.Second)
            {
                WorkflowRunTimestamp.SetSecondStamp(workflowInstanceId, stamp);
            }

            return directory;
        }

        private static string ResolveStamp(string workflowInstanceId, RuntimeDirectoryMode mode)
        {
            if (mode != RuntimeDirectoryMode.Second)
            {
                return FormatTimestamp(DateTime.Now, mode);
            }

            string stamp;
            if (WorkflowRunTimestamp.TryGetSecondStamp(workflowInstanceId, out stamp))
            {
                return stamp;
            }

            string logFile;
            if (LogMessageActivity.TryGetExistingLogFile(workflowInstanceId, out logFile)
                && WorkflowRunTimestamp.TryParseLogFileStamp(logFile, out stamp))
            {
                WorkflowRunTimestamp.SetSecondStamp(workflowInstanceId, stamp);
                return stamp;
            }

            return WorkflowRunTimestamp.GetOrCreateSecondStamp(workflowInstanceId);
        }

        private static string FormatTimestamp(DateTime value, RuntimeDirectoryMode mode)
        {
            switch (mode)
            {
                case RuntimeDirectoryMode.Year:
                    return value.ToString("yyyy");
                case RuntimeDirectoryMode.Month:
                    return value.ToString("yyyyMM");
                case RuntimeDirectoryMode.Day:
                    return value.ToString("yyyyMMdd");
                case RuntimeDirectoryMode.Hour:
                    return value.ToString("yyyyMMddHH");
                case RuntimeDirectoryMode.Minute:
                    return value.ToString("yyyyMMddHHmm");
                case RuntimeDirectoryMode.Second:
                default:
                    return value.ToString(WorkflowRunTimestamp.SecondStampFormat);
            }
        }

        private static bool TryResolveCurrentWorkflow(out string workflowInstanceId, out string projectName)
        {
            workflowInstanceId = null;
            projectName = null;

            object instance = FindCurrentWorkflowInstance();
            if (instance == null)
            {
                return false;
            }

            string localId = GetStringPropertyValue(instance, "InstanceId");
            string localProject = GetStringPropertyValue(instance, "projectname", "ProjectName");
            ResolveRootWorkflow(localId, localProject, out workflowInstanceId, out projectName);
            return !string.IsNullOrWhiteSpace(workflowInstanceId);
        }

        private static string ResolveProjectName(string workflowInstanceId)
        {
            object instance = FindWorkflowInstanceById(workflowInstanceId);
            object root = FindRootCallerInstance(instance) ?? instance;
            return root == null
                ? null
                : GetStringPropertyValue(root, "projectname", "ProjectName");
        }

        private static string ResolveRootInstanceKey(string workflowInstanceId)
        {
            string rootInstanceId;
            string unusedProject;
            ResolveRootWorkflow(workflowInstanceId, projectNameHint: null, out rootInstanceId, out unusedProject);
            return string.IsNullOrWhiteSpace(rootInstanceId) ? string.Empty : rootInstanceId.Trim();
        }

        /// <summary>
        /// Walk OpenRPA WorkflowInstance.caller to the outermost run; use that InstanceId + ProjectName.
        /// </summary>
        private static void ResolveRootWorkflow(
            string workflowInstanceId,
            string projectNameHint,
            out string rootInstanceId,
            out string rootProjectName)
        {
            rootInstanceId = string.IsNullOrWhiteSpace(workflowInstanceId)
                ? null
                : workflowInstanceId.Trim();
            rootProjectName = projectNameHint;

            object instance = FindWorkflowInstanceById(rootInstanceId);
            object root = FindRootCallerInstance(instance) ?? instance;
            if (root == null)
            {
                return;
            }

            string id = GetStringPropertyValue(root, "InstanceId");
            if (!string.IsNullOrWhiteSpace(id))
            {
                rootInstanceId = id.Trim();
            }

            string project = GetStringPropertyValue(root, "projectname", "ProjectName");
            if (!string.IsNullOrWhiteSpace(project))
            {
                rootProjectName = project;
            }
        }

        private static object FindRootCallerInstance(object startInstance)
        {
            object current = startInstance;
            for (int i = 0; i < MaxCallerDepth && current != null; i++)
            {
                string callerId = GetStringPropertyValue(current, "caller", "Caller");
                if (string.IsNullOrWhiteSpace(callerId))
                {
                    return current;
                }

                object parent = FindWorkflowInstanceById(callerId);
                if (parent == null)
                {
                    return current;
                }

                current = parent;
            }

            return current;
        }

        private static object FindCurrentWorkflowInstance()
        {
            var instances = GetWorkflowInstances();
            if (instances == null)
            {
                return null;
            }

            object fallback = null;
            foreach (object item in instances)
            {
                if (item == null)
                {
                    continue;
                }

                bool? completed = GetBoolPropertyValue(item, "isCompleted");
                string state = GetStringPropertyValue(item, "state");
                string instanceId = GetStringPropertyValue(item, "InstanceId");
                string rootKey = ResolveRootInstanceKey(instanceId);

                if (!string.IsNullOrWhiteSpace(rootKey) &&
                    PathsByWorkflowInstanceId.ContainsKey(rootKey))
                {
                    return item;
                }

                if (completed == false)
                {
                    if (string.Equals(state, "running", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(state, "idle", StringComparison.OrdinalIgnoreCase))
                    {
                        return item;
                    }

                    if (fallback == null)
                    {
                        fallback = item;
                    }
                }
            }

            return fallback;
        }

        private static object FindWorkflowInstanceById(string workflowInstanceId)
        {
            if (string.IsNullOrWhiteSpace(workflowInstanceId))
            {
                return null;
            }

            var instances = GetWorkflowInstances();
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

                string id = GetStringPropertyValue(item, "InstanceId");
                if (string.Equals(id, workflowInstanceId, StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }

            return null;
        }

        private static System.Collections.IEnumerable GetWorkflowInstances()
        {
            try
            {
                Type wfType = Type.GetType("OpenRPA.WorkflowInstance, OpenRPA", false);
                PropertyInfo instancesProp = wfType?.GetProperty("Instances", BindingFlags.Public | BindingFlags.Static);
                return instancesProp?.GetValue(null) as System.Collections.IEnumerable;
            }
            catch
            {
                return null;
            }
        }

        private static bool? GetBoolPropertyValue(object target, string propertyName)
        {
            if (target == null || string.IsNullOrWhiteSpace(propertyName))
            {
                return null;
            }

            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property == null)
            {
                return null;
            }

            object value = property.GetValue(target);
            if (value is bool)
            {
                return (bool)value;
            }

            return null;
        }

        private static string GetStringPropertyValue(object target, params string[] propertyNames)
        {
            if (target == null || propertyNames == null || propertyNames.Length == 0)
            {
                return null;
            }

            Type type = target.GetType();
            foreach (string propertyName in propertyNames)
            {
                PropertyInfo property = type.GetProperty(
                    propertyName,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (property == null)
                {
                    continue;
                }

                string text = property.GetValue(target) as string;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            return null;
        }

        private static string SanitizePathSegment(string value)
        {
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            return new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        }
    }
}
