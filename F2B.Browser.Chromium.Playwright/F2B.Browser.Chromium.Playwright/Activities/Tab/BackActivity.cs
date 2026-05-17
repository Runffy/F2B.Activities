using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Back")]
    [Description("Move the tab back in history.")]
    [Designer(typeof(CanvasFieldsActivityDesigner))]
    public sealed class BackActivity : CodeActivity
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
