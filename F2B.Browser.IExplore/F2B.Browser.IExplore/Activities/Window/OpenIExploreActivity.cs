using System;
using System.Activities;
using System.ComponentModel;
using F2B.Browser.IExplore.Com;

namespace F2B.Browser.IExplore
{
    [DisplayName("Open IExplore")]
    [Description("Start Trident IE via x86 ComHost (COM). Navigates when Url is set; does not wait for a window — use Find Window next.")]
    public sealed class OpenIExploreActivity : CodeActivity
    {
        [DisplayName("Url")]
        [Description("URL to open. Leave empty to start IE visible only (no navigation).")]
        [Category("Input")]
        public InArgument<string> Url { get; set; }

        [DisplayName("Apply IE Policy")]
        [Description("Apply localhost IE automation policy before launch.")]
        [Category("Input")]
        [DefaultValue(true)]
        public bool ApplyIePolicy { get; set; } = true;

        [DisplayName("Launch Method")]
        [Description("How IE was started (from ComHost).")]
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

            IeComHostRunner.Launch(url, out var method);
            LaunchMethod?.Set(context, method);
        }
    }
}
