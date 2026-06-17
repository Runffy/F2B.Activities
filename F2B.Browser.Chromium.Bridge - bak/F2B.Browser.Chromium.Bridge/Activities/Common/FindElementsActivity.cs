using System;
using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using F2B.Browser.Chromium.Bridge.Selectors;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Find Elements")]
    [Description("Instantly query all matching elements in a tab or parent element. Does not wait; returns a snapshot of current matches.")]
    [Designer(typeof(BridgeCanvasFieldsActivityDesigner))]
    public sealed class FindElementsActivity : CodeActivity
    {
        public FindElementsActivity()
        {
            DisplayName = "Find Elements";
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
        [Description("Selector XML used to locate matching elements.")]
        [RequiredArgument]
        [Category("Input.C")]
        public InArgument<string> Selector { get; set; }

        [DisplayName("Elements Result")]
        [Description("Outputs all currently matching elements (may be empty).")]
        [Category("Output")]
        public OutArgument<BwElement[]> ElementsResult { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var selector = Selector.Get(context);

            BwElement[] found;
            if (BaseOn == BridgeFindElementBaseOn.Tab)
            {
                var tab = Tab == null ? null : Tab.Get(context);
                if (!SelectorXmlSerializer.HasWndLevel(selector))
                    BridgeSelectorRules.EnsureTabOrWnd(selector, tab);

                found = BridgeElementLocator.FindAllBySelector(selector, tab);
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

                found = element.FindElements(selector);
            }

            if (ElementsResult != null && BridgeActivityArgumentHelper.HasExpression(ElementsResult))
                ElementsResult.Set(context, found ?? new BwElement[0]);
        }
    }
}
