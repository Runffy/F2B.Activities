using System.Activities;
using System.ComponentModel;
using System.Activities.Presentation;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("New Tab")]
    [Description("Create a new tab in the browser and return it.")]
    [Designer(typeof(BridgeCanvasFieldsActivityDesigner))]
    public sealed class NewTabActivity : CodeActivity
    {
        public NewTabActivity()
        {
            DisplayName = "New Tab";
        }

        [DisplayName("Input Browser")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<BwBrowser> Browser { get; set; }

        [DisplayName("Url")]
        [Category("Input.B")]
        public InArgument<string> Url { get; set; }

        [DisplayName("Timeout (ms)")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        [DisplayName("Tab")]
        [Category("Output")]
        public OutArgument<BwTab> Tab { get; set; }

        [DisplayName("Tab Info")]
        [Category("Output")]
        public OutArgument<BwTabInfo> TabInfo { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var tab = Browser.Get(context).NewTab(
                Url == null ? null : Url.Get(context),
                BridgeActivityArgumentHelper.GetOrDefault(Timeout, context, 15000));
            Tab?.Set(context, tab);
            TabInfo?.Set(context, tab.GetInfo());
        }
    }
}
