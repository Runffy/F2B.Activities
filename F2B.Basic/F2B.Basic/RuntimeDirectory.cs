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
    /// Usage: <c>F2B.Basic.RuntimeDirectory.Path</c> (always Second precision).
    /// </summary>
    public static class RuntimeDirectory
    {
        private static readonly ConcurrentDictionary<string, string> PathsByWorkflowInstanceId =
            new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Runtime directory for the current OpenRPA workflow run (timestamp precision: Second).
        /// Create on first access; reuse for later calls in the same run.
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

        /// <summary>
        /// Returns the runtime directory for the given workflow instance, creating it on first use.
        /// </summary>
        public static string GetOrCreate(
            string workflowInstanceId,
            string projectName,
            RuntimeDirectoryMode mode = RuntimeDirectoryMode.Second)
        {
            string key = string.IsNullOrWhiteSpace(workflowInstanceId)
                ? string.Empty
                : workflowInstanceId.Trim();

            return PathsByWorkflowInstanceId.GetOrAdd(key, _ => CreateDirectory(projectName, mode));
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

        private static string CreateDirectory(string projectName, RuntimeDirectoryMode mode)
        {
            string safeProjectName = SanitizePathSegment(
                string.IsNullOrWhiteSpace(projectName) ? "UnknownProject" : projectName.Trim());
            string stamp = FormatTimestamp(DateTime.Now, mode);
            string directory = System.IO.Path.Combine(
                Extensions.ProjectsDirectory,
                "Runtime",
                safeProjectName,
                stamp);

            Directory.CreateDirectory(directory);
            return directory;
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
                    return value.ToString("yyyyMMddhh");
                case RuntimeDirectoryMode.Minute:
                    return value.ToString("yyyyMMddhhmm");
                case RuntimeDirectoryMode.Second:
                default:
                    return value.ToString("yyyyMMddhhmmss");
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

            workflowInstanceId = GetStringPropertyValue(instance, "InstanceId");
            projectName = GetStringPropertyValue(instance, "projectname", "ProjectName");
            return !string.IsNullOrWhiteSpace(workflowInstanceId);
        }

        private static string ResolveProjectName(string workflowInstanceId)
        {
            object instance = FindWorkflowInstanceById(workflowInstanceId);
            return instance == null
                ? null
                : GetStringPropertyValue(instance, "projectname", "ProjectName");
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

                if (!string.IsNullOrWhiteSpace(instanceId) &&
                    PathsByWorkflowInstanceId.ContainsKey(instanceId))
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
