using System.Activities;
using System.ComponentModel;
using System.Activities.Presentation;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Get Activated Tab")]
    [Description("Get the currently active tab.")]
    [Designer(typeof(BridgeCanvasFieldsActivityDesigner))]
    public sealed class BrowserGetActivatedTabActivity : CodeActivity
    {
        public BrowserGetActivatedTabActivity()
        {
            DisplayName = "Get Activated Tab";
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
            Tab?.Set(context, Browser.Get(context).GetActivatedTab());
        }
    }
}
