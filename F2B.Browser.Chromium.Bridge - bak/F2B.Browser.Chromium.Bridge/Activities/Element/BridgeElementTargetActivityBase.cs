using System;
using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Element Target Base")]
    [Description("Provides unified target resolution for Bridge element activities.")]
    [Designer(typeof(BridgeElementTargetActivityDesigner))]
    [TypeDescriptionProvider(typeof(BridgeElementTargetTypeDescriptionProvider))]
    public abstract class BridgeElementTargetActivityBase : CodeActivity, IBridgeElementTargetConfig
    {
        protected BridgeElementTargetActivityBase(string displayName)
        {
            DisplayName = displayName;
        }

        [DisplayName("Target Type")]
        [Description("Choose whether to resolve target by element or selector.")]
        [Category("Input.A")]
        [DefaultValue(BridgeElementTargetType.Selector)]
        [TypeConverter("F2B.Browser.Chromium.Bridge.BridgeElementTargetTypeConverter, F2B.Browser.Chromium.Bridge")]
        public BridgeElementTargetType TargetType { get; set; } = BridgeElementTargetType.Selector;

        [DisplayName("Input Tab")]
        [Description("Optional tab instance. Required when Selector has no <wnd>; ignored when Selector contains <wnd> (wnd takes precedence).")]
        [Category("Input.B")]
        public InArgument<BwTab> InputTab { get; set; }

        [DisplayName("Selector")]
        [Description("Selector XML used to locate the target element. Include <wnd> when Input Tab is omitted.")]
        [Category("Input.B")]
        public InArgument<string> Selector { get; set; }

        [DisplayName("Input Element")]
        [Description("Element object to operate on directly.")]
        [Category("Input.C")]
        public InArgument<BwElement> Element { get; set; }

        [DisplayName("Delay Before (ms)")]
        [Description("Wait time in milliseconds before execution.")]
        [Category("Input.Z")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        BridgeElementTargetType IBridgeElementTargetConfig.TargetType => TargetType;

        protected BwElement ResolveTargetElement(CodeActivityContext context)
        {
            return ResolveTargetElement(context, 15000);
        }

        protected BwElement ResolveTargetElement(CodeActivityContext context, int timeoutMs)
        {
            var delayBefore = BridgeActivityArgumentHelper.GetOrDefault(DelayBefore, context, 300);

            if (TargetType == BridgeElementTargetType.Element)
            {
                var element = BridgeActivityArgumentHelper.GetBwElement(Element, context);
                if (element == null)
                {
                    throw new ArgumentException(
                        "Element must be provided when TargetType=Element. Variable '" +
                        BridgeActivityArgumentHelper.TryGetBoundVariableName(Element) +
                        "' is null or not assigned.");
                }

                BridgeDelay.Apply(delayBefore);
                return element;
            }

            var selector = Selector == null ? null : Selector.Get(context);
            if (string.IsNullOrWhiteSpace(selector))
                throw new ArgumentException("Selector must be provided when TargetType=Selector.");

            var inputTab = InputTab == null ? null : InputTab.Get(context);
            return BridgeElementLocator.FindBySelector(selector, inputTab, index: 0, timeoutMs, delayBefore);
        }

        protected BwElement ResolveTargetElementWithTimeout(CodeActivityContext context, InArgument<int> timeout, int defaultTimeoutMs = 15000)
        {
            var timeoutMs = BridgeActivityArgumentHelper.GetOrDefault(timeout, context, defaultTimeoutMs);
            return ResolveTargetElement(context, timeoutMs);
        }
    }
}
