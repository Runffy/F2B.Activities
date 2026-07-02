using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    public enum ElementTargetType
    {
        Element,
        Selector
    }

    [DisplayName("Element Target Base")]
    [Description("Provides unified target resolution for element activities.")]
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    [Designer(typeof(ElementTargetActivityDesigner))]
    public abstract class ElementTargetActivityBase : CodeActivity, IElementTargetConfig
    {
        protected ElementTargetActivityBase(string displayName)
        {
            DisplayName = displayName;
        }

        
        [DisplayName("Target Type")]
        [Description("Choose whether to resolve target by element or selector.")]
        [Category("Input.A")]
        [DefaultValue(ElementTargetType.Selector)]
        [TypeConverter("F2B.Browser.Chromium.Playwright.ElementTargetTypeConverter, F2B.Browser.Chromium.Playwright")]
        public ElementTargetType TargetType { get; set; } = ElementTargetType.Selector;

        [DisplayName("Input Tab")]
        [Description("Tab instance used when locating by selector.")]
        [Category("Input.B")]
        public InArgument<PwTab> InputTab { get; set; }

        [DisplayName("Selector")]
        [Description("Selector used to locate the target element.")]
        [Category("Input.B")]
        public InArgument<string> Selector { get; set; }




        [DisplayName("Input Element")]
        [Description("Element object to operate on directly.")]
        [Category("Input.C")]
        public InArgument<PwElement> Element { get; set; }

        

        

        

        [DisplayName("Delay Before (ms)")]
        [Description("Wait time in milliseconds before execution.")]
        [Category("Input.Z")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        protected PwElement ResolveTargetElement(CodeActivityContext context)
        {
            return ResolveTargetElement(context, null);
        }

        protected PwElement ResolveTargetElement(CodeActivityContext context, double? timeout)
        {
            var delayBefore = ActivityArgumentHelper.GetOrDefault(DelayBefore, context, 300);
            if (TargetType == ElementTargetType.Element)
            {
                var element = ActivityArgumentHelper.GetPwElement(Element, context);
                if (element == null)
                {
                    if (!ActivityArgumentHelper.HasExpression(Element))
                    {
                        throw new ArgumentException(
                            "Element must be provided when TargetType=Element. Assign a PwElement variable to the Element argument (for example elm_parent).");
                    }

                    throw new ArgumentException(
                        "Element must be provided when TargetType=Element. Variable '" +
                        ActivityArgumentHelper.TryGetBoundVariableName(Element) +
                        "' is null. Verify the upstream FindElement activity assigned ElementResult to the same variable in the same scope.");
                }

                PlaywrightSyncClient.ApplyDelay(delayBefore);
                return element;
            }

            var selector = Selector == null ? null : Selector.Get(context);
            if (string.IsNullOrWhiteSpace(selector))
            {
                throw new ArgumentException("Selector must be provided when TargetType=Selector.");
            }

            var tab = InputTab == null ? null : InputTab.Get(context);
            if (tab == null)
            {
                throw new ArgumentException("Tab must be provided when TargetType=Selector.");
            }

            var effectiveTimeout = timeout.HasValue ? Math.Max(0, timeout.Value) : (double?)null;
            var waitState = effectiveTimeout.HasValue ? "attached" : null;
            return tab.FindElement(selector, index: 0, timeout: effectiveTimeout, waitState: waitState, delayBefore: delayBefore);
        }

        protected PwElement ResolveTargetElementWithTimeout(CodeActivityContext context, InArgument<int> timeout, int defaultTimeoutMs = 15000)
        {
            var timeoutMs = ActivityArgumentHelper.GetOrDefault(timeout, context, defaultTimeoutMs);
            return ResolveTargetElement(context, timeoutMs);
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);
        }
    }
}
