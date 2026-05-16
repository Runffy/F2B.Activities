using System.Activities;
using System.ComponentModel;

namespace Playwright.Activities
{
    [Designer(typeof(CanvasFieldsActivityDesigner))]
    [DisplayName("Browser.GetLocalStorage")]
    public sealed class BrowserGetLocalStorageActivity : CodeActivity
    {
        [RequiredArgument]
        [Category("Input")]
        public InArgument<PwBrowser> Browser { get; set; }

        [Category("Output")]
        public OutArgument<Storages> Storage { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Storage?.Set(context, Browser.Get(context).GetLocalStorage());
        }
    }
}
