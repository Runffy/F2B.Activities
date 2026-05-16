using System.Activities;
using System.ComponentModel;

namespace Playwright.Activities
{
    [Designer(typeof(CanvasFieldsActivityDesigner))]
    [DisplayName("Tab.GetCookies")]
    public sealed class TabGetCookiesActivity : CodeActivity
    {
        [RequiredArgument]
        [Category("Input")]
        public InArgument<PwTab> Tab { get; set; }

        [Category("Output")]
        public OutArgument<Cookies> Cookies { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Cookies?.Set(context, Tab.Get(context).GetCookies());
        }
    }
}
