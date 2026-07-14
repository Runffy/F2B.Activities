using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Tab-New")]
    [Description("Create a new tab in the browser and optionally navigate to a URL.")]
    [Designer(typeof(CdpEmptyActivityDesigner))]
    public sealed class TabNewActivity : CodeActivity
    {
        public TabNewActivity()
        {
            DisplayName = "Tab-New";
        }

        [DisplayName("Port")]
        [Description("CDP port used to attach when Browser is not provided.")]
        [Category("Input.A")]
        public InArgument<int?> Port { get; set; }

        [DisplayName("Browser")]
        [Description("Browser instance.")]
        [Category("Input.A")]
        public InArgument<CdpBrowser> Browser { get; set; }

        [DisplayName("Url")]
        [Description("Optional URL to navigate to in the new tab.")]
        [Category("Input.B")]
        public InArgument<string> Url { get; set; }

        [DisplayName("Tab")]
        [Description("Outputs the new tab.")]
        [Category("Output")]
        public OutArgument<CdpTab> Tab { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var port = Port == null ? null : Port.Get(context);
            var browserArg = Browser == null ? null : Browser.Get(context);
            var browser = CdpBrowserLocator.Resolve(browserArg, port);
            var url = Url == null ? null : Url.Get(context);
            var tab = browser.NewTab(url);
            Tab?.Set(context, tab);
        }
    }
}
