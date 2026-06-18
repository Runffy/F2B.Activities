using System.Activities;
using System.ComponentModel;
using System.Activities.Presentation;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Get Tab Info")]
    [Description("Get tab information such as title and URL.")]
    [Designer(typeof(BridgeCanvasFieldsActivityDesigner))]
    public sealed class TabGetInfoActivity : CodeActivity
    {
        public TabGetInfoActivity()
        {
            DisplayName = "Get Tab Info";
        }

        [DisplayName("Input Tab")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<BwTab> Tab { get; set; }

        [DisplayName("Info")]
        [Category("Output")]
        public OutArgument<BwTabInfo> Info { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Info?.Set(context, Tab.Get(context).GetInfo());
        }
    }
}
