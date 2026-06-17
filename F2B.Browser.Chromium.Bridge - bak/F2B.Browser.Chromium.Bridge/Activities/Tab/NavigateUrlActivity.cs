using System.Activities;
using System.ComponentModel;
using System.Activities.Presentation;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Navigate Url")]
    [Description("Navigate the tab to a URL without waiting for the page to finish loading.")]
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

        protected override void Execute(CodeActivityContext context)
        {
            Tab.Get(context).NavigateUrl(Url.Get(context), waitForLoad: false);
        }
    }
}
