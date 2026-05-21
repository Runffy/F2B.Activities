using System;
using System.Activities;
using System.ComponentModel;
using F2B.Browser.IExplore.Com;

namespace F2B.Browser.IExplore
{
    [DisplayName("Open IExplore")]
    [Description("Start Trident IE via iexplore.exe (C:\\Program Files\\Internet Explorer\\iexplore.exe). Pass Url as argument when set; does not wait for a window — use Find Window next.")]
    public sealed class OpenIExploreActivity : CodeActivity
    {
        [DisplayName("Url")]
        [Description("URL passed to iexplore.exe. Leave empty to start IE without arguments.")]
        [Category("Input")]
        public InArgument<string> Url { get; set; }

        [DisplayName("Apply IE Policy")]
        [Description("Apply localhost IE automation policy before launch.")]
        [Category("Input")]
        [DefaultValue(true)]
        public bool ApplyIePolicy { get; set; } = true;

        [DisplayName("Launch Method")]
        [Description("Path used to start IE.")]
        [Category("Output")]
        public OutArgument<string> LaunchMethod { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            if (ApplyIePolicy)
                IeSecurityConfigurator.ApplyAutomationPolicy();

            var url = Url == null ? null : Url.Get(context);
            if (string.IsNullOrWhiteSpace(url))
                url = null;
            else
                url = url.Trim();

            IeLauncher.StartViaDefaultIExploreExe(url, out var method);
            LaunchMethod?.Set(context, method);
        }
    }
}
