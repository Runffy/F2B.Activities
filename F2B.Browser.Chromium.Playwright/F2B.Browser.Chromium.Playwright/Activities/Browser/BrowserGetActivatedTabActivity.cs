using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Get Activated Tab")]
    [Description("Get the currently active tab.")]
    [Designer(typeof(CanvasFieldsActivityDesigner))]
    public sealed class BrowserGetActivatedTabActivity : CodeActivity
    {
        [DisplayName("Input Browser")]
        [Description("Browser instance to read from.")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<PwBrowser> Browser { get; set; }

        [DisplayName("Tab")]
        [Description("Outputs the active tab instance.")]
        [Category("Output")]
        public OutArgument<PwTab> Tab { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Tab?.Set(context, Browser.Get(context).GetActivatedTab());
        }
    }
}
