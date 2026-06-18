using System.Activities;
using System.ComponentModel;
using System.Activities.Presentation;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Get All Tabs")]
    [Description("Get all tabs in the browser.")]
    [Designer(typeof(BridgeCanvasFieldsActivityDesigner))]
    public sealed class BrowserGetAllTabActivity : CodeActivity
    {
        public BrowserGetAllTabActivity()
        {
            DisplayName = "Get All Tabs";
        }

        [DisplayName("Input Browser")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<BwBrowser> Browser { get; set; }

        [DisplayName("Tabs")]
        [Category("Output")]
        public OutArgument<BwTab[]> Tabs { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Tabs?.Set(context, Browser.Get(context).GetAllTabs());
        }
    }
}
