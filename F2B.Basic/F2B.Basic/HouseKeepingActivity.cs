using OpenRPA.Interfaces;
using System;
using System.Activities;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;

namespace F2B.Basic
{
    [Designer(typeof(HouseKeepingDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("House Keeping")]
    [Description("Deletes current-project LogMessage csv files and Runtime folders whose name timestamps are strictly earlier than Before.")]
    public sealed class HouseKeepingActivity : CodeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        private static readonly Regex LogFileStampRegex = new Regex(
            @"^\[(?<stamp>\d{14})\].+\.csv$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex RuntimeFolderStampRegex = new Regex(
            @"^(?<stamp>\d{4,14})$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public HouseKeepingActivity()
        {
            DisplayName = "House Keeping";
        }

        [RequiredArgument]
        [DisplayName("Before")]
        [Description("Delete log csv files and runtime folders with name timestamps strictly earlier than this DateTime.")]
        [Category("Input.A")]
        public InArgument<DateTime> Before { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new HouseKeepingActivity();
        }

        protected override void Execute(CodeActivityContext context)
        {
            DateTime before = Before.Get(context);
            string projectName = ResolveProjectName(context) ?? "UnknownProject";
            string safeProject = SanitizeFileName(projectName);

            CleanupLogFiles(before, safeProject);
            CleanupRuntimeFolders(before, safeProject);
        }

        private static void CleanupLogFiles(DateTime before, string safeProjectName)
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "OpenRPA",
                "Logs");
            if (!Directory.Exists(folder))
            {
                return;
            }

            foreach (string file in Directory.EnumerateFiles(folder, "*.csv", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    string name = Path.GetFileName(file);
                    Match match = LogFileStampRegex.Match(name ?? string.Empty);
                    if (!match.Success)
                    {
                        continue;
                    }

                    // Only current project: [{stamp}]{project}.csv
                    string withoutExt = Path.GetFileNameWithoutExtension(name);
                    string expectedPrefix = "[" + match.Groups["stamp"].Value + "]" + safeProjectName;
                    if (!string.Equals(withoutExt, expectedPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    DateTime stampTime;
                    if (!WorkflowRunTimestamp.TryParseStampDateTime(match.Groups["stamp"].Value, out stampTime))
                    {
                        continue;
                    }

                    if (stampTime < before)
                    {
                        File.Delete(file);
                    }
                }
                catch
                {
                    // Skip locked / inaccessible files; continue with the rest.
                }
            }
        }

        private static void CleanupRuntimeFolders(DateTime before, string safeProjectName)
        {
            string projectRuntimeRoot = Path.Combine(Extensions.ProjectsDirectory, "Runtime", safeProjectName);
            if (!Directory.Exists(projectRuntimeRoot))
            {
                return;
            }

            foreach (string directory in Directory.EnumerateDirectories(projectRuntimeRoot))
            {
                try
                {
                    string name = Path.GetFileName(directory);
                    Match match = RuntimeFolderStampRegex.Match(name ?? string.Empty);
                    if (!match.Success)
                    {
                        continue;
                    }

                    DateTime stampTime;
                    if (!TryParseRuntimeStamp(match.Groups["stamp"].Value, out stampTime))
                    {
                        continue;
                    }

                    if (stampTime < before)
                    {
                        Directory.Delete(directory, recursive: true);
                    }
                }
                catch
                {
                    // Skip in-use folders; continue with the rest.
                }
            }
        }

        private static bool TryParseRuntimeStamp(string stamp, out DateTime value)
        {
            value = default(DateTime);
            if (string.IsNullOrWhiteSpace(stamp))
            {
                return false;
            }

            // Accept Year..Second folder name lengths produced by RuntimeDirectoryMode.
            string[] formats =
            {
                "yyyy",
                "yyyyMM",
                "yyyyMMdd",
                "yyyyMMddHH",
                "yyyyMMddHHmm",
                "yyyyMMddHHmmss",
                // Legacy 12-hour Second folders (hh).
                "yyyyMMddhhmmss"
            };

            return DateTime.TryParseExact(
                stamp,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out value);
        }

        private static string ResolveProjectName(CodeActivityContext context)
        {
            try
            {
                string instanceId = context.WorkflowInstanceId.ToString();
                Type wfType = Type.GetType("OpenRPA.WorkflowInstance, OpenRPA", false);
                PropertyInfo instancesProp = wfType?.GetProperty("Instances", BindingFlags.Public | BindingFlags.Static);
                var instances = instancesProp?.GetValue(null) as System.Collections.IEnumerable;
                if (instances != null)
                {
                    foreach (object item in instances)
                    {
                        if (item == null)
                        {
                            continue;
                        }

                        PropertyInfo idProp = item.GetType().GetProperty(
                            "InstanceId",
                            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                        string id = idProp?.GetValue(item) as string;
                        if (!string.Equals(id, instanceId, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        foreach (string name in new[] { "projectname", "ProjectName" })
                        {
                            PropertyInfo prop = item.GetType().GetProperty(
                                name,
                                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                            string project = prop?.GetValue(item) as string;
                            if (!string.IsNullOrWhiteSpace(project))
                            {
                                return project.Trim();
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "UnknownProject";
            }

            var invalid = Path.GetInvalidFileNameChars();
            return new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        }
    }
}
