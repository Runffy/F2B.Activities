using System.Activities;
using System.ComponentModel;
using System.Activities.Presentation;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Get Latest Tab")]
    [Description("Get the latest tab in the browser.")]
    [Designer(typeof(BridgeCanvasFieldsActivityDesigner))]
    public sealed class BrowserGetLatestTabActivity : CodeActivity
    {
        public BrowserGetLatestTabActivity()
        {
            DisplayName = "Get Latest Tab";
        }

        [DisplayName("Input Browser")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<BwBrowser> Browser { get; set; }

        [DisplayName("Tab")]
        [Category("Output")]
        public OutArgument<BwTab> Tab { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Tab?.Set(context, Browser.Get(context).GetLatestTab());
        }
    }
}
