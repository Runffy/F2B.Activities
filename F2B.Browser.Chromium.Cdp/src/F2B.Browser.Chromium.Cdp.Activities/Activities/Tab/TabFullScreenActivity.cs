using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Tab-FullScreen")]
    [Description("Enter browser fullscreen for the tab window (equivalent to pressing F11).")]
    [Designer(typeof(CdpEmptyActivityDesigner))]
    public sealed class TabFullScreenActivity : CodeActivity
    {
        public TabFullScreenActivity()
        {
            DisplayName = "Tab-FullScreen";
        }

        [DisplayName("Input Tab")]
        [Description("Tab to fullscreen. Optional when Selector contains <wnd>.")]
        [Category("Input.A")]
        public InArgument<CdpTab> Tab { get; set; }

        [DisplayName("Selector")]
        [Description("Optional window selector XML containing <wnd>. Use instead of Input Tab.")]
        [Category("Input.A")]
        public InArgument<string> Selector { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var selector = Selector == null ? null : Selector.Get(context);
            var inputTab = Tab == null ? null : Tab.Get(context);
            var tab = CdpTabLocator.Resolve(selector, inputTab);
            tab.Full();
        }
    }
}
