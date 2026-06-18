using System.Activities;
using System.ComponentModel;
using System.Activities.Presentation;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Forward")]
    [Description("Navigate forward in tab history.")]
    [Designer(typeof(BridgeCanvasFieldsActivityDesigner))]
    public sealed class ForwardActivity : CodeActivity
    {
        public ForwardActivity()
        {
            DisplayName = "Forward";
        }

        [DisplayName("Input Tab")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<BwTab> Tab { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Tab.Get(context).Forward();
        }
    }
}
