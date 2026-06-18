using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Close Tab")]
    [Description("Close the specified tab.")]
    [Designer(typeof(BridgeCanvasFieldsActivityDesigner))]
    public sealed class TabCloseActivity : CodeActivity
    {
        public TabCloseActivity()
        {
            DisplayName = "Close Tab";
        }

        [DisplayName("Input Tab")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<BwTab> Tab { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Tab.Get(context).Close();
        }
    }
}
