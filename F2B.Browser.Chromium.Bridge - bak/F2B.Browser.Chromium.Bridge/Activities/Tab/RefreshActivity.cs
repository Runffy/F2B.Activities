using System.Activities;
using System.ComponentModel;
using System.Activities.Presentation;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Refresh")]
    [Description("Refresh the current tab.")]
    [Designer(typeof(BridgeCanvasFieldsActivityDesigner))]
    public sealed class RefreshActivity : CodeActivity
    {
        public RefreshActivity()
        {
            DisplayName = "Refresh";
        }

        [DisplayName("Input Tab")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<BwTab> Tab { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Tab.Get(context).Refresh();
        }
    }
}
