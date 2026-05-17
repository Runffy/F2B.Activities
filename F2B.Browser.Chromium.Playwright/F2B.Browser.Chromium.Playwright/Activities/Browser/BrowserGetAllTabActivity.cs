using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Get All Tabs")]
    [Description("Get all tabs in the browser.")]
    [Designer(typeof(CanvasFieldsActivityDesigner))]
    public sealed class BrowserGetAllTabActivity : CodeActivity
    {
        [DisplayName("Input Browser")]
        [Description("Browser instance to read from.")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<PwBrowser> Browser { get; set; }

        [DisplayName("Tabs")]
        [Description("Outputs all tabs in the browser.")]
        [Category("Output")]
        public OutArgument<PwTab[]> Tabs { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Tabs?.Set(context, Browser.Get(context).GetAllTab());
        }
    }
}
