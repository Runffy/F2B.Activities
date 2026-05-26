using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Forward")]
    [Description("Move the tab forward in history.")]
    [Designer(typeof(CanvasFieldsActivityDesigner))]
    public sealed class ForwardActivity : CodeActivity
    {
        public ForwardActivity()
        {
            DisplayName = "Forward";
        }

        [DisplayName("Input Tab")]
        [Description("Tab instance to move forward.")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<PwTab> Tab { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Tab.Get(context).Forward();
        }
    }
}
