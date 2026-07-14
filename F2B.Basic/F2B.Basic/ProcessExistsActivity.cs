using System;
using System.Activities;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace F2B.Basic
{
    [Designer(typeof(ProcessExistsDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Process Exists")]
    [Description("Check whether a process is running by process name or full executable path.")]
    public sealed class ProcessExistsActivity : CodeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public ProcessExistsActivity()
        {
            DisplayName = "Process Exists";
        }

        [DisplayName("Process Name")]
        [Description("Process name without or with .exe (case-insensitive). Ignored when File Path is provided.")]
        [Category("Input.A")]
        public InArgument<string> ProcessName { get; set; }

        [DisplayName("File Path")]
        [Description("Full executable path (case-insensitive). Takes priority over Process Name when both are set.")]
        [Category("Input.A")]
        public InArgument<string> FilePath { get; set; }

        [DisplayName("Exists")]
        [Category("Output")]
        public OutArgument<bool> Exists { get; set; }

        [DisplayName("Count")]
        [Category("Output")]
        public OutArgument<int> Count { get; set; }

        [DisplayName("Paths")]
        [Description("Main module file paths of matched processes. Unavailable paths are empty strings.")]
        [Category("Output")]
        public OutArgument<string[]> Paths { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new ProcessExistsActivity();
        }

        protected override void Execute(CodeActivityContext context)
        {
            string processName = ProcessName == null ? null : ProcessName.Get(context);
            string filePath = FilePath == null ? null : FilePath.Get(context);

            bool hasName = !string.IsNullOrWhiteSpace(processName);
            bool hasPath = !string.IsNullOrWhiteSpace(filePath);
            if (!hasName && !hasPath)
            {
                throw new ArgumentException("Either Process Name or File Path is required.");
            }

            var matchedPaths = new List<string>();
            if (hasPath)
            {
                MatchByFilePath(filePath.Trim(), matchedPaths);
            }
            else
            {
                MatchByProcessName(processName.Trim(), matchedPaths);
            }

            int count = matchedPaths.Count;
            Exists?.Set(context, count > 0);
            Count?.Set(context, count);
            Paths?.Set(context, matchedPaths.ToArray());
        }

        private static void MatchByFilePath(string filePath, List<string> matchedPaths)
        {
            string normalizedTarget = NormalizePath(filePath);
            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    string path = TryGetMainModuleFileName(process);
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        continue;
                    }

                    if (string.Equals(NormalizePath(path), normalizedTarget, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedPaths.Add(path);
                    }
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        private static void MatchByProcessName(string processName, List<string> matchedPaths)
        {
            string normalizedName = NormalizeProcessName(processName);
            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    string currentName = NormalizeProcessName(process.ProcessName);
                    if (!string.Equals(currentName, normalizedName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    matchedPaths.Add(TryGetMainModuleFileName(process) ?? string.Empty);
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        private static string NormalizeProcessName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            string trimmed = name.Trim();
            if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(0, trimmed.Length - 4);
            }

            return trimmed;
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(path.Trim());
            }
            catch
            {
                return path.Trim();
            }
        }

        private static string TryGetMainModuleFileName(Process process)
        {
            try
            {
                return process.MainModule?.FileName ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
