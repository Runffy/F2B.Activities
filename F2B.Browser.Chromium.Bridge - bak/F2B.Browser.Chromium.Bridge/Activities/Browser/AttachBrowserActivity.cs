using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Attach Browser")]
    [Description("Attach to a running Chrome tab by matching a <wnd>-only selector (title/url).")]
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

        [DisplayName("Selector")]
        [Description("Wnd-only selector XML used to locate the target tab, e.g. <wnd title='...' /> or <wnd url='...' />. Optional idx selects the Nth match.")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<string> Selector { get; set; }

        [DisplayName("Output Browser")]
        [Description("Outputs the browser instance that owns the matched tab.")]
        [Category("Output")]
        public OutArgument<BwBrowser> Browser { get; set; }

        [DisplayName("Output Tab")]
        [Description("Outputs the matched tab. Activates the tab when it is not already active.")]
        [Category("Output")]
        public OutArgument<BwTab> Tab { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var selector = Selector == null ? null : Selector.Get(context);
            if (string.IsNullOrWhiteSpace(selector))
                throw new ArgumentException("Selector must be provided for Attach Browser.");

            var connectTimeoutMs = BridgeActivityArgumentHelper.GetOrDefault(ConnectTimeout, context, 60000);
            var attached = BridgeActivityServices.AttachByWndSelector(
                selector,
                TimeSpan.FromMilliseconds(connectTimeoutMs));

            Browser?.Set(context, attached.Browser);
            Tab?.Set(context, attached.Tab);
        }
    }
}
