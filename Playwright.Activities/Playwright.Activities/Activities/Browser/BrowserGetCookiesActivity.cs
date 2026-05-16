using System.Activities;
using System.ComponentModel;

namespace Playwright.Activities
{
    [Designer(typeof(CanvasFieldsActivityDesigner))]
    [DisplayName("Browser.GetCookies")]
    public sealed class BrowserGetCookiesActivity : CodeActivity
    {
        [RequiredArgument]
        [Category("Input")]
        public InArgument<PwBrowser> Browser { get; set; }

        [Category("Output")]
        public OutArgument<Cookies> Cookies { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Cookies?.Set(context, Browser.Get(context).GetCookies());
        }
    }
}
