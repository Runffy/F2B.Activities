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

        public LogMessageActivity()
        {
            Level = new InArgument<string>("INFO");
        }

        [Category("Identity")]
        [DisplayName("Log entry ID")]
        [Description("此活动实例的唯一标识（只读）。从工具箱拖入时自动生成。")]
        [ReadOnly(true)]
        [Browsable(true)]
        public string LogEntryId { get; set; }

        [Editor(typeof(LogLevelOptionsEditor), typeof(global::System.Activities.Presentation.PropertyEditing.ExtendedPropertyValueEditor))]
        [RequiredArgument]
        public InArgument<string> Level { get; set; }

        [RequiredArgument]
        public InArgument<object> Message { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new LogMessageActivity
            {
                LogEntryId = Guid.NewGuid().ToString("D"),
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

            if (!File.Exists(logfile))
            {
                File.AppendAllText(logfile, "Timestamp,WorkflowName,LogEntryId,Level,Message" + Environment.NewLine);
            }

            string entryId = string.IsNullOrWhiteSpace(LogEntryId) ? string.Empty : LogEntryId.Trim();

            string line = string.Join(",",
                EscapeCsv(DateTime.Now.ToString("o", CultureInfo.InvariantCulture)),
                EscapeCsv(workflowName),
                EscapeCsv(entryId),
                EscapeCsv(level),
                EscapeCsv(message));
            File.AppendAllText(logfile, line + Environment.NewLine);

            string consoleLine = $"[{level}] {message}";
            Console.WriteLine(consoleLine);
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
