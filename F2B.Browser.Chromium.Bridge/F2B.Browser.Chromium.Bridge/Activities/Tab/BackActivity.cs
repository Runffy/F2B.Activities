using System.Activities;
using System.ComponentModel;
using System.Activities.Presentation;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Back")]
    [Description("Navigate back in tab history.")]
    [Designer(typeof(BridgeCanvasFieldsActivityDesigner))]
    public sealed class BackActivity : CodeActivity
    {
        public BackActivity()
        {
            DisplayName = "Back";
        }

        [DisplayName("Input Tab")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<BwTab> Tab { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Tab.Get(context).Back();
        }
    }
}
