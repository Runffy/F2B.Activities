using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Get Tab Info")]
    [Description("Get tab information such as title and URL.")]
    [Designer(typeof(CanvasFieldsActivityDesigner))]
    public sealed class TabGetInfoActivity : CodeActivity
    {
        [DisplayName("Input Tab")]
        [Description("Tab instance to read.")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<PwTab> Tab { get; set; }

        [DisplayName("Info")]
        [Description("Outputs tab information.")]
        [Category("Output")]
        public OutArgument<TabInfo> Info { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Info?.Set(context, Tab.Get(context).GetInfo());
        }
    }
}
