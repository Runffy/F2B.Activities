using System.Activities;
using System.ComponentModel;
using System.Activities.Presentation;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Close Browser")]
    [Description("Close the browser window opened by Open Browser.")]
    [Designer(typeof(BridgeCanvasFieldsActivityDesigner))]
    public sealed class BrowserCloseActivity : CodeActivity
    {
        public BrowserCloseActivity()
        {
            DisplayName = "Close Browser";
        }

        [DisplayName("Input Browser")]
        [Description("Browser instance to close.")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<BwBrowser> Browser { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Browser.Get(context).BrowserClose();
        }
    }
}
