using System.Activities;
using System.ComponentModel;

namespace Playwright.Activities
{
    [Designer(typeof(CanvasFieldsActivityDesigner))]
    [DisplayName("Browser.GetAllTab")]
    public sealed class BrowserGetAllTabActivity : CodeActivity
    {
        [RequiredArgument]
        [Category("Input")]
        public InArgument<PwBrowser> Browser { get; set; }

        [Category("Output")]
        public OutArgument<PwTab[]> Tabs { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Tabs?.Set(context, Browser.Get(context).GetAllTab());
        }
    }
}
