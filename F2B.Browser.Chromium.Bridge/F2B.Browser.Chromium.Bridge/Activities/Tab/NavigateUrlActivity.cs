using System.Activities;
using System.ComponentModel;
using System.Activities.Presentation;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Navigate Url")]
    [Description("Navigate the tab to a URL.")]
    [Designer(typeof(BridgeCanvasFieldsActivityDesigner))]
    public sealed class NavigateUrlActivity : CodeActivity
    {
        public NavigateUrlActivity()
        {
            DisplayName = "Navigate Url";
        }

        [DisplayName("Input Tab")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<BwTab> Tab { get; set; }

        [DisplayName("Url")]
        [RequiredArgument]
        [Category("Input.B")]
        public InArgument<string> Url { get; set; }

        [DisplayName("Timeout (ms)")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            Tab.Get(context).NavigateUrl(
                Url.Get(context),
                BridgeActivityArgumentHelper.GetOrDefault(Timeout, context, 15000));
        }
    }
}
