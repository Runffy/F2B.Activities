using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Tab-GetSpecific")]
    [Description("Find a specific tab using selector XML.")]
    [Designer(typeof(CdpCanvasFieldsActivityDesigner))]
    public sealed class TabGetSpecificActivity : CodeActivity
    {
        public TabGetSpecificActivity()
        {
            DisplayName = "Tab-GetSpecific";
        }

        [DisplayName("Selector")]
        [Description("Selector XML with <wnd> used to locate the tab.")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<string> Selector { get; set; }

        [DisplayName("Tab")]
        [Description("Outputs the matched tab.")]
        [Category("Output")]
        public OutArgument<CdpTab> Tab { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var selector = Selector.Get(context);
            var tab = CdpTabLocator.ResolveRequired(selector, null);
            Tab?.Set(context, tab);
        }
    }
}
