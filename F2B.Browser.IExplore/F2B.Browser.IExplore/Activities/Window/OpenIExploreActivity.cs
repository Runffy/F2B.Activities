using System;
using System.Activities;
using System.ComponentModel;
using F2B.Browser.IExplore.Com;

namespace F2B.Browser.IExplore
{
    [DisplayName("Open IExplore")]
    [Description("Launch Trident IE via x86 IExplore.ComHost.exe (COM), then attach with Find Window.")]
    public sealed class OpenIExploreActivity : CodeActivity
    {
        [DisplayName("Url")]
        [Description("URL to open. Leave empty for a blank page.")]
        [Category("Input")]
        public InArgument<string> Url { get; set; }

        [DisplayName("Title Contains")]
        [Description("Window title filter while waiting for IE (default: IExplore Test Host).")]
        [Category("Input")]
        [DefaultValue(IeComHostRunner.DefaultTitlePart)]
        public InArgument<string> TitleContains { get; set; }

        [DisplayName("Url Contains")]
        [Description("Optional URL fragment to match while waiting (e.g. demo.html).")]
        [Category("Input")]
        public InArgument<string> UrlContains { get; set; }

        [DisplayName("Timeout (ms)")]
        [Category("Input")]
        [DefaultValue(45000)]
        public InArgument<int> Timeout { get; set; } = 45000;

        [DisplayName("Apply IE Policy")]
        [Description("Apply localhost IE automation policy before launch.")]
        [Category("Input")]
        [DefaultValue(true)]
        public bool ApplyIePolicy { get; set; } = true;

        [DisplayName("Launch Method")]
        [Description("How IE was started (from ComHost).")]
        [Category("Output")]
        public OutArgument<string> LaunchMethod { get; set; }

        [DisplayName("Window Handle")]
        [Description("HWND of the IE browser frame (decimal).")]
        [Category("Output")]
        public OutArgument<long> WindowHandle { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            if (ApplyIePolicy)
                IeSecurityConfigurator.ApplyAutomationPolicy();

            var url = Url == null ? null : Url.Get(context);
            if (string.IsNullOrWhiteSpace(url))
                url = "about:blank";

            var titlePart = TitleContains == null
                ? IeComHostRunner.DefaultTitlePart
                : TitleContains.Get(context);
            var urlContains = UrlContains == null ? null : UrlContains.Get(context);
            var timeout = ActivityArgumentHelper.GetOrDefault(Timeout, context, 45000);

            IeComHostRunner.Launch(url, out var method, out var hwnd, timeout, titlePart, urlContains);

            LaunchMethod?.Set(context, method);
            WindowHandle?.Set(context, hwnd);
        }
    }
}
