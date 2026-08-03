using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Browser-Close")]
    [Description("Close the browser process when this instance launched it.")]
    [Designer(typeof(CdpEmptyActivityDesigner))]
    public sealed class BrowserCloseActivity : CodeActivity
    {
        public BrowserCloseActivity()
        {
            DisplayName = "Browser-Close";
        }

        [DisplayName("Port")]
        [Description("CDP port used to attach when Browser is not provided.")]
        [Category("Input.A")]
        public InArgument<int?> Port { get; set; }

        [DisplayName("Browser")]
        [Description("Browser instance to close.")]
        [Category("Input.A")]
        public InArgument<CdpBrowser> Browser { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var port = Port == null ? null : Port.Get(context);
            var browserArg = Browser == null ? null : Browser.Get(context);
            var browser = CdpBrowserLocator.Resolve(browserArg, port);
            browser.Quit();
        }
    }
}
