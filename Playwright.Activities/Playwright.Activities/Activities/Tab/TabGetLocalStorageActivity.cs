using System.Activities;
using System.ComponentModel;

namespace Playwright.Activities
{
    [Designer(typeof(CanvasFieldsActivityDesigner))]
    [DisplayName("Tab.GetLocalStorage")]
    public sealed class TabGetLocalStorageActivity : CodeActivity
    {
        [RequiredArgument]
        [Category("Input")]
        public InArgument<PwTab> Tab { get; set; }

        [Category("Output")]
        public OutArgument<Storages> Storage { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Storage?.Set(context, Tab.Get(context).GetLocalStorage());
        }
    }
}
