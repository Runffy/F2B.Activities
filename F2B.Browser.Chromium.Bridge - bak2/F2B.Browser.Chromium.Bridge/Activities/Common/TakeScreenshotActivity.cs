using System;
using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Bridge
{
    public interface IBridgeTakeScreenshotConfig
    {
        BridgeTakeScreenshotBaseOn BaseOn { get; }
    }

    [DisplayName("Take Screenshot")]
    [Description("Capture a screenshot of a tab or target element.")]
    [Designer(typeof(BridgeCanvasFieldsActivityDesigner))]
    [TypeDescriptionProvider(typeof(BridgeTakeScreenshotTypeDescriptionProvider))]
    public sealed class TakeScreenshotActivity : CodeActivity, IBridgeTakeScreenshotConfig, IBridgeElementTargetConfig
    {
        public TakeScreenshotActivity()
        {
            DisplayName = "Take Screenshot";
        }

        [DisplayName("Base On")]
        [Category("Input.A")]
        [DefaultValue(BridgeTakeScreenshotBaseOn.Element)]
        public BridgeTakeScreenshotBaseOn BaseOn { get; set; } = BridgeTakeScreenshotBaseOn.Element;

        [DisplayName("Input Tab")]
        [Description("Optional when Selector contains <wnd>.")]
        [Category("Input.B")]
        public InArgument<BwTab> InputTab { get; set; }

        [DisplayName("Target Type")]
        [Category("Input.C")]
        [DefaultValue(BridgeElementTargetType.Selector)]
        [TypeConverter("F2B.Browser.Chromium.Bridge.BridgeElementTargetTypeConverter, F2B.Browser.Chromium.Bridge")]
        public BridgeElementTargetType TargetType { get; set; } = BridgeElementTargetType.Selector;

        [DisplayName("Selector")]
        [Category("Input.E")]
        public InArgument<string> Selector { get; set; }

        [DisplayName("Input Element")]
        [Category("Input.E")]
        public InArgument<BwElement> InputElement { get; set; }

        [DisplayName("Path")]
        [RequiredArgument]
        [Category("Input.F")]
        public InArgument<string> Path { get; set; }

        [DisplayName("Full Page")]
        [Category("Input.G")]
        [DefaultValue(true)]
        [TypeConverter("F2B.Browser.Chromium.Bridge.BridgeBooleanTypeConverter, F2B.Browser.Chromium.Bridge")]
        public bool FullPage { get; set; } = true;

        [DisplayName("Delay Before (ms)")]
        [Category("Input.Z")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        [DisplayName("Timeout (ms)")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        BridgeElementTargetType IBridgeElementTargetConfig.TargetType => TargetType;

        protected override void Execute(CodeActivityContext context)
        {
            var path = Path.Get(context);
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException("Path is required.");

            if (BaseOn == BridgeTakeScreenshotBaseOn.Tab)
            {
                var tab = InputTab == null ? null : InputTab.Get(context);
                if (tab == null)
                    throw new InvalidOperationException("InputTab must be provided when BaseOn=Tab.");
                tab.TakeScreenshot(path, FullPage, BridgeActivityArgumentHelper.GetOrDefault(Timeout, context, 15000));
                return;
            }

            var budget = new TimeoutBudget(BridgeActivityArgumentHelper.GetOrDefault(Timeout, context, 15000));
            var element = BridgeCompositeTargetResolver.ResolveElementTarget(
                context, TargetType, InputElement, Selector, InputTab, DelayBefore, budget.RemainingMs);
            element.TakeScreenshot(path, budget.RemainingMs);
        }
    }
}
