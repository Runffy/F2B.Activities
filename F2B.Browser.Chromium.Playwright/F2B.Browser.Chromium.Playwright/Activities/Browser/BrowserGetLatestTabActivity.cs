using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Get Latest Tab")]
    [Description("Get the most recently created or activated tab.")]
    [Designer(typeof(CanvasFieldsActivityDesigner))]
    public sealed class BrowserGetLatestTabActivity : CodeActivity
    {
        public BrowserGetLatestTabActivity()
        {
            DisplayName = "Get Latest Tab";
        }

        [DisplayName("Input Browser")]
        [Description("Browser instance to read from.")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<PwBrowser> Browser { get; set; }

        [DisplayName("Tab")]
        [Description("Outputs the latest tab instance.")]
        [Category("Output")]
        public OutArgument<PwTab> Tab { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Tab?.Set(context, Browser.Get(context).GetLatestTab());
        }
    }
}
