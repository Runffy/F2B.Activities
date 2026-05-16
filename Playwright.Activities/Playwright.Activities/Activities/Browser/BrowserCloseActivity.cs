using System.Activities;
using System.ComponentModel;

namespace Playwright.Activities
{
    [Designer(typeof(CanvasFieldsActivityDesigner))]
    [DisplayName("Browser.Close")]
    public sealed class BrowserCloseActivity : CodeActivity
    {
        [RequiredArgument]
        [Category("Input")]
        public InArgument<PwBrowser> Browser { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Browser.Get(context).Close();
        }
    }
}
