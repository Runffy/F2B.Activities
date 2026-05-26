using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Close Browser")]
    [Description("Close the specified browser instance.")]
    [Designer(typeof(CanvasFieldsActivityDesigner))]
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
        public InArgument<PwBrowser> Browser { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Browser.Get(context).Close();
        }
    }
}
