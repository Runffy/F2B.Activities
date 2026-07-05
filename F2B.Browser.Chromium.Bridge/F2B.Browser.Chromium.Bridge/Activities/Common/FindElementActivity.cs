using System;
using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using F2B.Browser.Chromium.Bridge.Selectors;

namespace F2B.Browser.Chromium.Bridge
{
    public enum BridgeFindElementBaseOn
    {
        Tab,
        Element
    }

    [DisplayName("Find Element")]
    [Description("Find a matching element in a tab or parent element using selector XML. Retries within the timeout when the page is still loading or transient errors occur.")]
    [Designer(typeof(BridgeCanvasFieldsActivityDesigner))]
    public sealed class FindElementActivity : CodeActivity
    {
        public FindElementActivity()
        {
            DisplayName = "Find Element";
        }

        [DisplayName("Base On")]
        [Description("Choose whether to search from a tab or parent element.")]
        [Category("Input.A")]
        [DefaultValue(BridgeFindElementBaseOn.Tab)]
        public BridgeFindElementBaseOn BaseOn { get; set; } = BridgeFindElementBaseOn.Tab;

        [DisplayName("Input Tab")]
        [Description("Tab instance used for element search. Optional when Selector contains <wnd>; ignored when Selector contains <wnd>.")]
        [Category("Input.B")]
        public InArgument<BwTab> Tab { get; set; }

        [DisplayName("Input Element")]
        [Description("Parent element used as the search root.")]
        [Category("Input.B")]
        public InArgument<BwElement> Element { get; set; }

        [DisplayName("Selector")]
        [Description("Selector XML used to locate the target element.")]
        [RequiredArgument]
        [Category("Input.C")]
        public InArgument<string> Selector { get; set; }

        [DisplayName("Index")]
        [Description("Index used when multiple elements match (0-based).")]
        [Category("Input.C")]
        [DefaultValue(0)]
        public InArgument<int> Index { get; set; } = 0;

        [DisplayName("Timeout (ms)")]
        [Description("Timeout in milliseconds for element search.")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        [DisplayName("Delay Before (ms)")]
        [Description("Wait time in milliseconds before starting search.")]
        [Category("Input.Z")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        [DisplayName("Element Result")]
        [Description("Outputs the found element.")]
        [Category("Output")]
        public OutArgument<BwElement> ElementResult { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var selector = Selector.Get(context);
            var index = BridgeActivityArgumentHelper.GetOrDefault(Index, context, 0);
            var timeoutMs = BridgeActivityArgumentHelper.GetOrDefault(Timeout, context, 15000);
            var delayBefore = BridgeActivityArgumentHelper.GetOrDefault(DelayBefore, context, 300);

            BwElement found;
            if (BaseOn == BridgeFindElementBaseOn.Tab)
            {
                var tab = Tab == null ? null : Tab.Get(context);
                if (!SelectorXmlSerializer.HasWndLevel(selector))
                    BridgeSelectorRules.EnsureTabOrWnd(selector, tab);

                found = BridgeElementLocator.FindBySelector(
                    selector,
                    tab,
                    index,
                    timeoutMs,
                    delayBefore);
            }
            else
            {
                var element = BridgeActivityArgumentHelper.GetBwElement(Element, context);
                if (element == null)
                {
                    if (!BridgeActivityArgumentHelper.HasExpression(Element))
                        throw new InvalidOperationException("Element must be provided when BaseOn=Element.");

                    throw new InvalidOperationException(
                        "Element must be provided when BaseOn=Element. The Element argument expression evaluated to null.");
                }

                found = element.FindElement(
                    selector,
                    index,
                    timeoutMs,
                    BridgeFindElementWaitState.Attached,
                    delayBefore);
            }

            BridgeActivityArgumentHelper.SetBwElement(ElementResult, context, found);
        }
    }
}
