using System.Activities;
using System.ComponentModel;

namespace Playwright.Activities
{
    [Designer(typeof(CanvasFieldsActivityDesigner))]
    [DisplayName("Tab.GetInfo")]
    public sealed class TabGetInfoActivity : CodeActivity
    {
        [RequiredArgument]
        [Category("Input")]
        public InArgument<PwTab> Tab { get; set; }

        [Category("Output")]
        public OutArgument<TabInfo> Info { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Info?.Set(context, Tab.Get(context).GetInfo());
        }
    }
}
