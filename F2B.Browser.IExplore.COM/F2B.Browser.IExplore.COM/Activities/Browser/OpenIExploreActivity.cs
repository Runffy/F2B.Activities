using System;
using System.Activities;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace F2B.Browser.IExplore.COM
{
    [DisplayName("Open IExplore")]
    [Description("Launch iexplore.exe optionally with URL. Does not wait for process exit.")]
    [Designer(typeof(RequiredFieldsActivityDesigner))]
    public sealed class OpenIExploreActivity : CodeActivity
    {
        [Category("Input")]
        [DisplayName("Url")]
        public InArgument<string> Url { get; set; }

        [Category("Input")]
        [DisplayName("IExplore Path")]
        [DefaultValue(@"C:\Program Files\Internet Explorer\iexplore.exe")]
        public InArgument<string> IExplorePath { get; set; } = @"C:\Program Files\Internet Explorer\iexplore.exe";

        [Category("Time")]
        [DisplayName("Delay Before")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        [Category("Output")]
        [DisplayName("Process Id")]
        public OutArgument<int> ProcessId { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ActivityArgumentHelper.ApplyDelayBefore(DelayBefore, context, 300);

            var iePath = IExplorePath == null ? null : IExplorePath.Get(context);
            if (string.IsNullOrWhiteSpace(iePath))
                iePath = @"C:\Program Files\Internet Explorer\iexplore.exe";

            if (!File.Exists(iePath))
                throw new FileNotFoundException("iexplore.exe not found.", iePath);

            var url = Url == null ? null : Url.Get(context);
            var psi = new ProcessStartInfo
            {
                FileName = iePath,
                Arguments = string.IsNullOrWhiteSpace(url) ? string.Empty : url,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(iePath)
            };

            var process = Process.Start(psi);
            ProcessId.Set(context, process == null ? 0 : process.Id);
        }
    }
}
