using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Refresh")]
    [Description("Refresh the specified tab.")]
    [Designer(typeof(CanvasFieldsActivityDesigner))]
    public sealed class RefreshActivity : CodeActivity
    {
        public RefreshActivity()
        {
            DisplayName = "Refresh";
        }

        [DisplayName("Input Tab")]
        [Description("Tab instance to refresh.")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<PwTab> Tab { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Tab.Get(context).Refresh();
        }
    }
}
