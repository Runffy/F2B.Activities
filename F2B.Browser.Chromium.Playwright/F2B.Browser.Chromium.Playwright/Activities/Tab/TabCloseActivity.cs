using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Close Tab")]
    [Description("Close the specified tab.")]
    [Designer(typeof(CanvasFieldsActivityDesigner))]
    public sealed class TabCloseActivity : CodeActivity
    {
        [DisplayName("Input Tab")]
        [Description("Tab instance to close.")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<PwTab> Tab { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Tab.Get(context).Close();
        }
    }
}
