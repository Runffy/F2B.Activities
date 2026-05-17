using System;
using System.Activities;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Windows;

namespace F2B.OS
{
    [Designer(typeof(RunCmdCommandDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Run CMD Command")]
    public sealed class RunCmdCommandActivity : CodeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public RunCmdCommandActivity()
        {
            WaitForCompletion = new InArgument<bool>(true);
            ShowWindow = new InArgument<bool>(false);
        }

        [RequiredArgument]
        [DisplayName("Command")]
        public InArgument<string> Command { get; set; }

        [DisplayName("Wait for completion")]
        public InArgument<bool> WaitForCompletion { get; set; }

        [DisplayName("Show CMD window")]
        public InArgument<bool> ShowWindow { get; set; }

        [DisplayName("Result")]
        public OutArgument<string> Result { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new RunCmdCommandActivity
            {
                WaitForCompletion = new InArgument<bool>(true),
                ShowWindow = new InArgument<bool>(false)
            };
        }

        protected override void Execute(CodeActivityContext context)
        {
            string command = (Command.Get(context) ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(command))
            {
                throw new ArgumentException("Command is required.", nameof(Command));
            }

            bool waitForCompletion = WaitForCompletion.Get(context);
            bool showWindow = ShowWindow.Get(context);

            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c " + command,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = !showWindow,
                WindowStyle = showWindow ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden
            };

            using (var process = new Process { StartInfo = startInfo })
            {
                process.Start();

                if (!waitForCompletion)
                {
                    Result.Set(context, "Command started without waiting.");
                    return;
                }

                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                var output = new StringBuilder();
                if (!string.IsNullOrEmpty(stdout))
                {
                    output.Append(stdout);
                }

                if (!string.IsNullOrEmpty(stderr))
                {
                    if (output.Length > 0)
                    {
                        output.AppendLine();
                    }

                    output.Append(stderr);
                }

                Result.Set(context, output.ToString());
            }
        }
    }
}
