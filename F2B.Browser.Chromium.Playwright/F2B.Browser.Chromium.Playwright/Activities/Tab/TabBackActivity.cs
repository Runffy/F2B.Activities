using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Back Tab")]
    [Description("Move the tab back in history.")]
    [Designer(typeof(CanvasFieldsActivityDesigner))]
    public sealed class TabBackActivity : CodeActivity
    {
        [DisplayName("Input Tab")]
        [Description("Tab instance to move back.")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<PwTab> Tab { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Tab.Get(context).Back();
        }
    }
}
