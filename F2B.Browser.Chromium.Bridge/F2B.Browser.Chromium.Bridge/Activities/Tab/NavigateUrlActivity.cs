using System.Activities;
using System.ComponentModel;
using System.Activities.Presentation;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Navigate Url")]
    [Description("Navigate the tab to a URL. Optionally wait until the page load completes.")]
    [Designer(typeof(BridgeCanvasFieldsActivityDesigner))]
    public sealed class NavigateUrlActivity : CodeActivity
    {
        public NavigateUrlActivity()
        {
            DisplayName = "Navigate Url";
            WaitForLoadComplete = true;
            Timeout = 15000;
        }

        [DisplayName("Input Tab")]
        [Description("Tab to navigate. Optional when Selector contains <wnd>.")]
        [Category("Input.A")]
        public InArgument<BwTab> Tab { get; set; }

        [DisplayName("Selector")]
        [Description("Optional window selector XML containing <wnd> tags only. Use instead of Input Tab.")]
        [Category("Input.A")]
        public InArgument<string> Selector { get; set; }

        [DisplayName("Url")]
        [Description("Destination URL.")]
        [RequiredArgument]
        [Category("Input.B")]
        public InArgument<string> Url { get; set; }

        [DisplayName("Wait For Load Complete")]
        [Description("When true, wait until the page finishes loading before the activity completes.")]
        [Category("Input.C")]
        [DefaultValue(true)]
        public InArgument<bool> WaitForLoadComplete { get; set; }

        [DisplayName("Timeout (ms)")]
        [Description("Maximum wait time when Wait For Load Complete is true.")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var selector = Selector == null ? null : Selector.Get(context);
            var tab = Tab == null ? null : Tab.Get(context);
            var url = Url.Get(context);
            var waitForLoad = BridgeActivityArgumentHelper.GetOrDefault(WaitForLoadComplete, context, true);
            var timeoutMs = BridgeActivityArgumentHelper.GetOrDefault(Timeout, context, 15000);

            BridgeTabLocator.Resolve(selector, tab)
                .NavigateUrl(url, waitForLoad, timeoutMs);
        }
    }
}
