using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Attach Browser")]
    [Description("Attach to a connected Chromium Bridge extension and output the currently activated tab.")]
    public sealed class AttachBrowserActivity : CodeActivity
    {
        public AttachBrowserActivity()
        {
            DisplayName = "Attach Browser";
        }

        [DisplayName("Connect Timeout (ms)")]
        [Description("Maximum wait time for the Bridge extension to connect.")]
        [Category("Input")]
        [DefaultValue(60000)]
        public InArgument<int> ConnectTimeout { get; set; } = 60000;

        [DisplayName("Extension Instance Id")]
        [Description("Optional. Use when multiple Bridge extensions are connected.")]
        [Category("Input")]
        public InArgument<string> InstanceId { get; set; }

        [DisplayName("Output Browser")]
        [Description("Outputs the attached Bridge browser instance.")]
        [Category("Output")]
        public OutArgument<BwBrowser> Browser { get; set; }

        [DisplayName("Output Tab")]
        [Description("Outputs the currently activated tab.")]
        [Category("Output")]
        public OutArgument<BwTab> Tab { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var connectTimeoutMs = BridgeActivityArgumentHelper.GetOrDefault(ConnectTimeout, context, 60000);
            var instanceId = InstanceId == null ? null : InstanceId.Get(context);

            var browser = BridgeActivityServices.GetBrowser(
                instanceId,
                TimeSpan.FromMilliseconds(connectTimeoutMs));

            var activatedTab = browser.GetActivatedTab();

            Browser?.Set(context, browser);
            Tab?.Set(context, activatedTab);
        }
    }
}
