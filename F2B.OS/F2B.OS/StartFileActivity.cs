using System;
using System.Activities;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;

namespace F2B.OS
{
    [Designer(typeof(StartFileDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Start File")]
    public sealed class StartFileActivity : CodeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public StartFileActivity()
        {
            Operation = new InArgument<string>("open");
            WaitForExit = new InArgument<bool>(false);
            ShowWindow = new InArgument<bool>(true);
        }

        [RequiredArgument]
        [DisplayName("Path")]
        [Description("File path, folder path, or URL to open.")]
        public InArgument<string> Path { get; set; }

        [DisplayName("Operation")]
        [Description("Shell verb, e.g. open, edit, print.")]
        public InArgument<string> Operation { get; set; }

        [DisplayName("Arguments")]
        [Description("Optional command-line arguments for executable targets.")]
        public InArgument<string> Arguments { get; set; }

        [DisplayName("Working directory")]
        public InArgument<string> WorkingDirectory { get; set; }

        [DisplayName("Wait for exit")]
        public InArgument<bool> WaitForExit { get; set; }

        [DisplayName("Show window")]
        public InArgument<bool> ShowWindow { get; set; }

        [DisplayName("Result")]
        [Description("Result message, including PID when available.")]
        public OutArgument<string> Result { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new StartFileActivity
            {
                Operation = new InArgument<string>("open"),
                WaitForExit = new InArgument<bool>(false),
                ShowWindow = new InArgument<bool>(true)
            };
        }

        protected override void Execute(CodeActivityContext context)
        {
            string target = (Path.Get(context) ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(target))
            {
                throw new ArgumentException("Path is required.", nameof(Path));
            }

            string operation = (Operation.Get(context) ?? "open").Trim();
            string arguments = Arguments.Get(context);
            string workingDirectory = WorkingDirectory.Get(context);
            bool waitForExit = WaitForExit.Get(context);
            bool showWindow = ShowWindow.Get(context);

            var startInfo = new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true,
                Verb = string.IsNullOrWhiteSpace(operation) ? "open" : operation,
                WindowStyle = showWindow ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden
            };

            if (!string.IsNullOrWhiteSpace(arguments))
            {
                startInfo.Arguments = arguments;
            }

            if (!string.IsNullOrWhiteSpace(workingDirectory))
            {
                startInfo.WorkingDirectory = workingDirectory;
            }

            using (Process process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    Result.Set(context, "Started without process handle.");
                    return;
                }

                if (waitForExit)
                {
                    process.WaitForExit();
                    Result.Set(context, "Exited with code " + process.ExitCode);
                    return;
                }

                Result.Set(context, "Started. PID=" + process.Id);
            }
        }
    }
}
