using OpenRPA.Interfaces;
using System;
using System.Activities;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;

namespace F2B.Basic
{
    [Designer(typeof(LogMessageDesigner), typeof(global::System.ComponentModel.Design.IDesigner))]
    [DisplayName("Log Message")]
    public sealed class LogMessageActivity : CodeActivity, global::System.Activities.Presentation.IActivityTemplateFactory
    {
        private static readonly ConcurrentDictionary<string, string> WorkflowLogFiles = new ConcurrentDictionary<string, string>();
        private static readonly string MachineHostName = Environment.MachineName ?? string.Empty;
        private static readonly string MachineUserName = Environment.UserName ?? string.Empty;
        private const string UserLogHeader = "Timestamp,MachineName,UserName,WorkflowName,LogEntryId,Level,Message";
        private const string ExecutionLogHeader = "Timestamp,MachineName,UserName,ProjectName,WorkflowName,LogEntryId,Level,Message";

        public LogMessageActivity()
        {
            DisplayName = "Log Message";
            Level = new InArgument<string>("INFO");
        }

        [Category("Identity")]
        [DisplayName("Log entry ID")]
        [Description("Unique ID for this activity instance. Auto-generated from the toolbox, backfilled when empty, and regenerated on paste when IdRef changes. Do not edit manually.")]
        [Browsable(true)]
        public string LogEntryId { get; set; }

        internal static string CreateNewLogEntryId()
        {
            return Guid.NewGuid().ToString("D");
        }

        [Editor(typeof(LogLevelOptionsEditor), typeof(global::System.Activities.Presentation.PropertyEditing.ExtendedPropertyValueEditor))]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<string> Level { get; set; }

        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<object> Message { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new LogMessageActivity
            {
                LogEntryId = CreateNewLogEntryId(),
                Level = new InArgument<string>("INFO")
            };
        }

        protected override void Execute(CodeActivityContext context)
        {
            string level = (Level.Get(context) ?? "INFO").Trim().ToUpperInvariant();
            if (level != "INFO" && level != "WARN" && level != "ERROR")
            {
                level = "INFO";
            }

            object raw = Message.Get(context);
            string message = raw == null ? string.Empty : Convert.ToString(raw, CultureInfo.InvariantCulture);

            WorkflowMetadata workflowMetadata = ResolveWorkflowMetadata(context);
            string projectName = workflowMetadata.ProjectName ?? "UnknownProject";
            string workflowName = workflowMetadata.WorkflowName ?? "UnknownWorkflow";
            string instanceId = context.WorkflowInstanceId.ToString();
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "OpenRPA", "Logs");
            Directory.CreateDirectory(folder);

            string logfile = WorkflowLogFiles.GetOrAdd(instanceId, _ =>
            {
                string filename = $"[{DateTime.Now:yyyyMMddHHmmss}]{SanitizeFileName(projectName)}.csv";
                return Path.Combine(folder, filename);
            });

            string entryId = string.IsNullOrWhiteSpace(LogEntryId) ? string.Empty : LogEntryId.Trim();
            DateTime now = DateTime.Now;

            string userLine = BuildLogLine(now, workflowName, entryId, level, message);
            AppendLogLine(logfile, UserLogHeader, userLine);

            string executionLogFile = GetExecutionLogPath(now);
            string executionLine = BuildLogLine(now, workflowName, entryId, level, message, projectName);
            AppendLogLine(executionLogFile, ExecutionLogHeader, executionLine);

            string consoleLine = $"[{level}] {message}";
            Console.WriteLine(consoleLine);
        }

        private static string GetExecutionLogPath(DateTime timestamp)
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OpenRPA",
                "Logs");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, $"ExecutionLog_{timestamp:yyyyMMdd}.csv");
        }

        private static void AppendLogLine(string logFilePath, string header, string line)
        {
            if (!File.Exists(logFilePath))
            {
                File.AppendAllText(logFilePath, header + Environment.NewLine);
            }

            File.AppendAllText(logFilePath, line + Environment.NewLine);
        }

        private static string BuildLogLine(DateTime timestamp, string workflowName, string entryId, string level, string message, string projectName = null)
        {
            string timestampText = EscapeCsv(timestamp.ToString("o", CultureInfo.InvariantCulture));
            if (projectName == null)
            {
                return string.Join(",",
                    timestampText,
                    EscapeCsv(MachineHostName),
                    EscapeCsv(MachineUserName),
                    EscapeCsv(workflowName),
                    EscapeCsv(entryId),
                    EscapeCsv(level),
                    EscapeCsv(message));
            }

            return string.Join(",",
                timestampText,
                EscapeCsv(MachineHostName),
                EscapeCsv(MachineUserName),
                EscapeCsv(projectName),
                EscapeCsv(workflowName),
                EscapeCsv(entryId),
                EscapeCsv(level),
                EscapeCsv(message));
        }

        private static WorkflowMetadata ResolveWorkflowMetadata(CodeActivityContext context)
        {
            try
            {
                string workflowInstanceId = context.WorkflowInstanceId.ToString();
                Type wfType = Type.GetType("OpenRPA.WorkflowInstance, OpenRPA", false);
                if (wfType == null)
                {
                    return WorkflowMetadata.Empty;
                }

                PropertyInfo instancesProp = wfType.GetProperty("Instances", BindingFlags.Public | BindingFlags.Static);
                if (instancesProp == null)
                {
                    return WorkflowMetadata.Empty;
                }

                var instances = instancesProp.GetValue(null) as global::System.Collections.IEnumerable;
                if (instances == null)
                {
                    return WorkflowMetadata.Empty;
                }

                foreach (object item in instances)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    PropertyInfo instanceIdProp = item.GetType().GetProperty("InstanceId");
                    string id = instanceIdProp?.GetValue(item) as string;
                    if (!string.Equals(id, workflowInstanceId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string projectName = GetStringPropertyValue(item, "projectname", "ProjectName");

                    PropertyInfo workflowProp = item.GetType().GetProperty("Workflow");
                    object workflow = workflowProp?.GetValue(item);
                    string workflowName = GetStringPropertyValue(workflow, "name", "Name");

                    return new WorkflowMetadata(projectName, workflowName);
                }
            }
            catch
            {
                // Keep logging robust; fallback name is handled by caller.
            }

            return WorkflowMetadata.Empty;
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
                PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (property == null)
                {
                    continue;
                }

                object value = property.GetValue(target);
                string text = value as string;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            return null;
        }

        private static string SanitizeFileName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        }

        private static string EscapeCsv(string value)
        {
            if (value == null)
            {
                return "\"\"";
            }

            string escaped = value.Replace("\"", "\"\"");
            return $"\"{escaped}\"";
        }

        private sealed class WorkflowMetadata
        {
            public static readonly WorkflowMetadata Empty = new WorkflowMetadata(null, null);

            public WorkflowMetadata(string projectName, string workflowName)
            {
                ProjectName = projectName;
                WorkflowName = workflowName;
            }

            public string ProjectName { get; private set; }

            public string WorkflowName { get; private set; }
        }
    }

    /// <summary>
    /// Binds LogEntryId to WorkflowViewState.IdRef for the current designer process.
    /// Same EntryId + new IdRef means paste; same IdRef means move/reload.
    /// Registry is updated only via Bind() after the model value is successfully written.
    /// </summary>
    internal static class LogEntryIdIdentity
    {
        private static readonly ConcurrentDictionary<string, string> EntryIdToIdRef =
            new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

        public static string Resolve(string entryId, string idRef, out bool changed)
        {
            changed = false;
            string normalizedEntryId = string.IsNullOrWhiteSpace(entryId) ? string.Empty : entryId.Trim();
            string normalizedIdRef = string.IsNullOrWhiteSpace(idRef) ? string.Empty : idRef.Trim();

            if (normalizedEntryId.Length == 0)
            {
                changed = true;
                return LogMessageActivity.CreateNewLogEntryId();
            }

            if (normalizedIdRef.Length == 0)
            {
                return normalizedEntryId;
            }

            string existingIdRef;
            if (EntryIdToIdRef.TryGetValue(normalizedEntryId, out existingIdRef))
            {
                if (string.Equals(existingIdRef, normalizedIdRef, StringComparison.Ordinal))
                {
                    return normalizedEntryId;
                }

                changed = true;
                return LogMessageActivity.CreateNewLogEntryId();
            }

            return normalizedEntryId;
        }

        public static void Bind(string entryId, string idRef)
        {
            if (string.IsNullOrWhiteSpace(entryId) || string.IsNullOrWhiteSpace(idRef))
            {
                return;
            }

            EntryIdToIdRef[entryId.Trim()] = idRef.Trim();
        }
    }

    internal sealed class LogLevelOptionsEditor : CustomSelectEditor
    {
        public override DataTable options
        {
            get
            {
                var t = new DataTable();
                t.Columns.Add("ID", typeof(string));
                t.Columns.Add("TEXT", typeof(string));
                t.Rows.Add("INFO", "INFO");
                t.Rows.Add("WARN", "WARN");
                t.Rows.Add("ERROR", "ERROR");
                return t;
            }
        }
    }
}
