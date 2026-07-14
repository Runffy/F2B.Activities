using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Tab-Activate")]
    [Description("Activate a tab by instance or selector.")]
    [Designer(typeof(CdpEmptyActivityDesigner))]
    public sealed class TabActivateActivity : CodeActivity
    {
        public TabActivateActivity()
        {
            DisplayName = "Tab-Activate";
        }

        [DisplayName("Tab")]
        [Description("Tab instance to activate.")]
        [Category("Input.A")]
        public InArgument<CdpTab> Tab { get; set; }

        [DisplayName("Selector")]
        [Description("Selector XML with <wnd> used to locate the tab.")]
        [Category("Input.A")]
        public InArgument<string> Selector { get; set; }

        [DisplayName("Tab")]
        [Description("Outputs the activated tab.")]
        [Category("Output")]
        public OutArgument<CdpTab> ActivatedTab { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var selector = Selector == null ? null : Selector.Get(context);
            var tab = Tab == null ? null : Tab.Get(context);
            var resolvedTab = CdpTabLocator.Resolve(selector, tab);
            resolvedTab.Browser.ActivateTab(resolvedTab);
            ActivatedTab?.Set(context, resolvedTab);
        }
    }
}
