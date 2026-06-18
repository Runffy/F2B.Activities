using System.Activities;
using System.ComponentModel;
using System.Activities.Presentation;
using F2B.Browser.Chromium.Bridge.Selectors;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Element Exists")]
    [Description("Check whether a matching element exists.")]
    [Designer(typeof(BridgeCanvasFieldsActivityDesigner))]
    public sealed class ElementExistsActivity : CodeActivity
    {
        public ElementExistsActivity()
        {
            DisplayName = "Element Exists";
        }

        [DisplayName("Input Tab")]
        [Description("Optional when Selector contains <wnd>.")]
        [Category("Input.A")]
        public InArgument<BwTab> Tab { get; set; }

        [DisplayName("Selector")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<string> Selector { get; set; }

        [DisplayName("Index")]
        [Category("Input.B")]
        [DefaultValue(0)]
        public InArgument<int> Index { get; set; } = 0;

        [DisplayName("Exists")]
        [Category("Output")]
        public OutArgument<bool> Exists { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var selector = Selector.Get(context);
            var tab = Tab == null ? null : Tab.Get(context);
            if (!SelectorXmlSerializer.HasWndLevel(selector))
                BridgeSelectorRules.EnsureTabOrWnd(selector, tab);

            var exists = BridgeElementLocator.Exists(
                selector,
                tab,
                BridgeActivityArgumentHelper.GetOrDefault(Index, context, 0));
            Exists?.Set(context, exists);
        }
    }
}
