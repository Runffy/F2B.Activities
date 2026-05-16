using System.Activities;
using System.ComponentModel;

namespace Playwright.Activities
{
    [Designer(typeof(CanvasFieldsActivityDesigner))]
    [DisplayName("Tab.Close")]
    public sealed class TabCloseActivity : CodeActivity
    {
        [RequiredArgument]
        [Category("Input")]
        public InArgument<PwTab> Tab { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Tab.Get(context).Close();
        }
    }
}
