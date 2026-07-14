using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using System.Linq;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Tab-GetAll")]
    [Description("Get all visible tabs in the browser.")]
    [Designer(typeof(CdpEmptyActivityDesigner))]
    public sealed class TabGetAllActivity : CodeActivity
    {
        public TabGetAllActivity()
        {
            DisplayName = "Tab-GetAll";
        }

        [DisplayName("Port")]
        [Description("CDP port used to attach when Browser is not provided.")]
        [Category("Input.A")]
        public InArgument<int?> Port { get; set; }

        [DisplayName("Browser")]
        [Description("Browser instance.")]
        [Category("Input.A")]
        public InArgument<CdpBrowser> Browser { get; set; }

        [DisplayName("Tabs")]
        [Description("Outputs all visible tabs.")]
        [Category("Output")]
        public OutArgument<CdpTab[]> Tabs { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var port = Port == null ? null : Port.Get(context);
            var browserArg = Browser == null ? null : Browser.Get(context);
            var browser = CdpBrowserLocator.Resolve(browserArg, port);
            var tabs = browser.GetTabs().ToArray();
            Tabs?.Set(context, tabs);
        }
    }
}
