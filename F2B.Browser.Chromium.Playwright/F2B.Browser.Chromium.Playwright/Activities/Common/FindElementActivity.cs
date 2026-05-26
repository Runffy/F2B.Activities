using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    public enum FindElementBaseOn
    {
        Tab,
        Element
    }

    [DisplayName("Find Element")]
    [Description("Find a matching element in a tab or parent element.")]
    [Designer(typeof(CanvasFieldsActivityDesigner))]
    public sealed class FindElementActivity : CodeActivity
    {
        public FindElementActivity()
        {
            DisplayName = "Find Element";
        }

        [DisplayName("Base On")]
        [Description("Choose whether to search from a tab or parent element.")]
        [Category("Input.A")]
        [DefaultValue(FindElementBaseOn.Tab)]
        public FindElementBaseOn BaseOn { get; set; } = FindElementBaseOn.Tab;

        [DisplayName("Input Tab")]
        [Description("Tab instance used for element search.")]
        [Category("Input.B")]
        public InArgument<PwTab> Tab { get; set; }

        [DisplayName("Input Element")]
        [Description("Parent element used as the search root.")]
        [Category("Input.B")]
        public InArgument<PwElement> Element { get; set; }

        [DisplayName("Selector")]
        [Description("Selector used to locate the target element.")]
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
        public InArgument<int?> Timeout { get; set; } = 15000;

        [DisplayName("Wait State")]
        [Description("Element state to wait for during search.")]
        [Category("Input.D")]
        [DefaultValue(FindElementWaitState.None)]
        public FindElementWaitState WaitState { get; set; } = FindElementWaitState.None;

        [DisplayName("Delay Before")]
        [Description("Wait time in milliseconds before starting search.")]
        [Category("Input.Z")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        [DisplayName("Element Result")]
        [Description("Outputs the found element.")]
        [Category("Output")]
        public OutArgument<PwElement> ElementResult { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var selector = Selector.Get(context);
            var index = ActivityArgumentHelper.GetOrDefault(Index, context, 0);
            var timeout = Timeout == null ? null : (double?)Timeout.Get(context);
            var waitState = ActivityArgumentHelper.ToWaitStateString(WaitState);
            var delayBefore = ActivityArgumentHelper.GetOrDefault(DelayBefore, context, 300);

            PwElement found;
            if (BaseOn == FindElementBaseOn.Tab)
            {
                var tab = Tab == null ? null : Tab.Get(context);
                if (tab == null)
                {
                    throw new InvalidOperationException("Tab must be provided when BaseOn=Tab.");
                }

                found = tab.FindElement(selector, index, timeout, waitState, delayBefore);
            }
            else
            {
                var element = ActivityArgumentHelper.GetPwElement(Element, context);
                if (element == null)
                {
                    if (!ActivityArgumentHelper.HasExpression(Element))
                    {
                        throw new InvalidOperationException("Element must be provided when BaseOn=Element.");
                    }

                    throw new InvalidOperationException("Element must be provided when BaseOn=Element. The Element argument expression evaluated to null.");
                }

                found = element.FindElement(selector, index, timeout, waitState, delayBefore);
            }

            ActivityArgumentHelper.SetPwElement(ElementResult, context, found);
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);

            if (Selector == null || Selector.Expression == null)
            {
                metadata.AddValidationError("Selector is required.");
            }

            if (BaseOn == FindElementBaseOn.Tab)
            {
                if (Tab == null || Tab.Expression == null)
                {
                    metadata.AddValidationError("Tab must be provided when BaseOn=Tab.");
                }
            }
        }
    }
}
