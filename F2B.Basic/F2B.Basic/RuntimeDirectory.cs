using OpenRPA.Interfaces;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;

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
    /// Source project/instance always come from <see cref="OpenRpaSourceWorkflow"/> (shared with
    /// <see cref="ResourceDirectory"/> and <c>F2B.Global</c>).
    /// Usage: <c>F2B.Basic.RuntimeDirectory.Path</c> (always Second precision).
    /// </summary>
    public static class RuntimeDirectory
    {
        private static readonly ConcurrentDictionary<string, string> PathsByWorkflowInstanceId =
            new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Runtime directory for the current OpenRPA workflow run (timestamp precision: Second).
        /// </summary>
        public static string Path
        {
            get
            {
                string instanceId;
                string projectName;
                OpenRpaSourceWorkflow.TryResolveCurrent(out instanceId, out projectName);
                return GetOrCreate(instanceId, projectName, RuntimeDirectoryMode.Second);
            }
        }

        public static bool TryGetExistingPath(string workflowInstanceId, out string runtimeDirectory)
        {
            string key = OpenRpaSourceWorkflow.ResolveSourceInstanceIdFromLocal(workflowInstanceId);
            return PathsByWorkflowInstanceId.TryGetValue(key, out runtimeDirectory)
                   && !string.IsNullOrWhiteSpace(runtimeDirectory);
        }

        public static string GetOrCreate(
            string workflowInstanceId,
            string projectName,
            RuntimeDirectoryMode mode = RuntimeDirectoryMode.Second)
        {
            string rootInstanceId;
            string rootProjectName;
            OpenRpaSourceWorkflow.TryResolve(
                workflowInstanceId,
                projectName,
                out rootInstanceId,
                out rootProjectName);

            string key = string.IsNullOrWhiteSpace(rootInstanceId)
                ? string.Empty
                : rootInstanceId.Trim();

            return PathsByWorkflowInstanceId.GetOrAdd(key, _ => CreateDirectory(key, rootProjectName, mode));
        }

        public static string GetOrCreate(
            System.Activities.CodeActivityContext context,
            RuntimeDirectoryMode mode = RuntimeDirectoryMode.Second)
        {
            string instanceId;
            string projectName;
            OpenRpaSourceWorkflow.TryResolve(context, out instanceId, out projectName);
            return GetOrCreate(instanceId, projectName, mode);
        }

        /// <summary>Delegates to <see cref="OpenRpaSourceWorkflow.ResolveSourceProjectName(System.Activities.CodeActivityContext)"/>.</summary>
        public static string ResolveSourceProjectName(System.Activities.CodeActivityContext context)
        {
            return OpenRpaSourceWorkflow.ResolveSourceProjectName(context);
        }

        /// <summary>Delegates to <see cref="OpenRpaSourceWorkflow.ResolveSourceProjectName()"/>.</summary>
        public static string ResolveSourceProjectName()
        {
            return OpenRpaSourceWorkflow.ResolveSourceProjectName();
        }

        /// <summary>Delegates to <see cref="OpenRpaSourceWorkflow.ResolveSourceInstanceId(System.Activities.CodeActivityContext)"/>.</summary>
        public static string ResolveSourceInstanceId(System.Activities.CodeActivityContext context)
        {
            return OpenRpaSourceWorkflow.ResolveSourceInstanceId(context);
        }

        /// <summary>Delegates to <see cref="OpenRpaSourceWorkflow.ResolveSourceInstanceId()"/>.</summary>
        public static string ResolveSourceInstanceId()
        {
            return OpenRpaSourceWorkflow.ResolveSourceInstanceId();
        }

        /// <summary>Delegates to <see cref="OpenRpaSourceWorkflow.IsSourceInstanceActive"/>.</summary>
        internal static bool IsSourceInstanceActive(string sourceInstanceId)
        {
            return OpenRpaSourceWorkflow.IsSourceInstanceActive(sourceInstanceId);
        }

        /// <summary>Delegates to <see cref="OpenRpaSourceWorkflow.GetActiveSourceInstanceIds"/>.</summary>
        internal static System.Collections.Generic.HashSet<string> GetActiveSourceInstanceIds()
        {
            return OpenRpaSourceWorkflow.GetActiveSourceInstanceIds();
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

        private static string SanitizePathSegment(string value)
        {
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            return new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        }
    }
}
