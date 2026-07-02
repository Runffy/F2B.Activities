using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    public enum TakeScreenshotBaseOn
    {
        Tab,
        Element
    }

    public interface ITakeScreenshotConfig
    {
        TakeScreenshotBaseOn BaseOn { get; }
    }

    [DisplayName("Take Screenshot")]
    [Description("Capture a screenshot of a tab or target element.")]
    [Designer(typeof(CanvasFieldsActivityDesigner))]
    [TypeDescriptionProvider(typeof(TakeScreenshotTypeDescriptionProvider))]
    public sealed class TakeScreenshotActivity : CodeActivity, ITakeScreenshotConfig, IElementTargetConfig
    {
        public TakeScreenshotActivity()
        {
            DisplayName = "Take Screenshot";
        }

        [DisplayName("Base On")]
        [Description("Choose whether to capture a tab page or a target element.")]
        [Category("Input.A")]
        [DefaultValue(TakeScreenshotBaseOn.Element)]
        public TakeScreenshotBaseOn BaseOn
        {
            get => _baseOn;
            set
            {
                _baseOn = value;
                TypeDescriptor.Refresh(this);
            }
        }

        [DisplayName("Input Tab")]
        [Description("Tab instance used for tab screenshot or selector-based element screenshot.")]
        [Category("Input.B")]
        public InArgument<PwTab> InputTab { get; set; }

        [DisplayName("Target Type")]
        [Description("Specify whether to target by element or selector.")]
        [Category("Input.C")]
        [DefaultValue(ElementTargetType.Selector)]
        [TypeConverter("F2B.Browser.Chromium.Playwright.ElementTargetTypeConverter, F2B.Browser.Chromium.Playwright")]
        public ElementTargetType TargetType
        {
            get => _targetType;
            set
            {
                _targetType = value;
                TypeDescriptor.Refresh(this);
            }
        }


        [DisplayName("Selector")]
        [Description("Selector used when locating the target element.")]
        [Category("Input.E")]
        public InArgument<string> Selector { get; set; }

        [DisplayName("Input Element")]
        [Description("Element object used directly as screenshot target.")]
        [Category("Input.E")]
        public InArgument<PwElement> InputElement { get; set; }

        [DisplayName("Path")]
        [Description("File path where the screenshot is saved.")]
        [RequiredArgument]
        [Category("Input.F")]
        public InArgument<string> Path { get; set; }

        [DisplayName("Full Page")]
        [Description("When BaseOn=Tab, capture the full scrollable page instead of the visible viewport.")]
        [Category("Input.G")]
        [DefaultValue(true)]
        [TypeConverter("F2B.Browser.Chromium.Playwright.BooleanTypeConverter, F2B.Browser.Chromium.Playwright")]
        public bool FullPage { get; set; } = true;

        [DisplayName("Delay Before (ms)")]
        [Description("Wait time in milliseconds before locating the target element.")]
        [Category("Input.Z")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        [DisplayName("Timeout (ms)")]
        [Description("Timeout in milliseconds for locating the target element.")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        private TakeScreenshotBaseOn _baseOn = TakeScreenshotBaseOn.Element;
        private ElementTargetType _targetType = ElementTargetType.Selector;

        protected override void Execute(CodeActivityContext context)
        {
            var path = Path.Get(context);
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidOperationException("Path is required.");
            }

            if (BaseOn == TakeScreenshotBaseOn.Tab)
            {
                var tab = InputTab == null ? null : InputTab.Get(context);
                if (tab == null)
                {
                    throw new InvalidOperationException("InputTab must be provided when BaseOn=Tab.");
                }

                tab.TakeScreenshot(path, FullPage);
                return;
            }

            var totalMs = ActivityArgumentHelper.GetOrDefault(Timeout, context, 15000);
            var budget = new TimeoutBudget(totalMs);
            ResolveElementTarget(context, budget.RemainingAsNullableDouble()).TakeScreenshot(path);
        }

        private PwElement ResolveElementTarget(CodeActivityContext context, double? locateTimeout)
        {
            var delayBefore = ActivityArgumentHelper.GetOrDefault(DelayBefore, context, 300);

            if (TargetType == ElementTargetType.Element)
            {
                var element = ActivityArgumentHelper.GetPwElement(InputElement, context);
                if (element == null)
                {
                    if (!ActivityArgumentHelper.HasExpression(InputElement))
                    {
                        throw new InvalidOperationException(
                            "InputElement must be provided when BaseOn=Element and TargetType=Element.");
                    }

                    throw new InvalidOperationException(
                        "InputElement must be provided when BaseOn=Element and TargetType=Element. Variable '" +
                        ActivityArgumentHelper.TryGetBoundVariableName(InputElement) +
                        "' is null.");
                }

                PlaywrightSyncClient.ApplyDelay(delayBefore);
                return element;
            }

            var selector = Selector == null ? null : Selector.Get(context);
            if (string.IsNullOrWhiteSpace(selector))
            {
                throw new InvalidOperationException("Selector must be provided when BaseOn=Element and TargetType=Selector.");
            }

            var tab = InputTab == null ? null : InputTab.Get(context);
            if (tab == null)
            {
                throw new InvalidOperationException("InputTab must be provided when BaseOn=Element and TargetType=Selector.");
            }

            var effectiveTimeout = locateTimeout.HasValue ? Math.Max(0, locateTimeout.Value) : (double?)null;
            var waitState = effectiveTimeout.HasValue ? "attached" : null;
            return tab.FindElement(selector, index: 0, timeout: effectiveTimeout, waitState: waitState, delayBefore: delayBefore);
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);

            if (Path == null || Path.Expression == null)
            {
                metadata.AddValidationError("Path is required.");
            }

            if (BaseOn == TakeScreenshotBaseOn.Tab &&
                (InputTab == null || InputTab.Expression == null))
            {
                metadata.AddValidationError("InputTab must be provided when BaseOn=Tab.");
            }
        }
    }
}
