using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore
{
    public enum IeElementTargetType
    {
        Element,
        Locator
    }

    public abstract class ElementTargetActivityBase : CodeActivity
    {
        [DisplayName("IE Window")]
        [Description("Embedded IE window instance.")]
        [Category("Target")]
        public InArgument<EmbeddedIEWindow> InputWindow { get; set; }

        [DisplayName("Target Type")]
        [Description("Operate on an element handle or locate by JSON.")]
        [Category("Target")]
        [DefaultValue(IeElementTargetType.Locator)]
        public IeElementTargetType TargetType { get; set; } = IeElementTargetType.Locator;

        [DisplayName("Element")]
        [Description("Existing element handle (Target Type = Element).")]
        [Category("Target")]
        public InArgument<IEHtmlElement> Element { get; set; }

        [DisplayName("Element (Json String)")]
        [Description("Element locator JSON object, e.g. {'id':'username','value':'hello'}")]
        [Category("Target")]
        public InArgument<string> ElementJson { get; set; }

        [DisplayName("Frame (Json String)")]
        [Description("Nested frame path JSON array, or empty for root document.")]
        [Category("Target")]
        public InArgument<string> FramePath { get; set; }

        [DisplayName("Delay Before")]
        [Description("Wait time in milliseconds before execution.")]
        [Category("Time")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        [DisplayName("Timeout (ms)")]
        [Description("Timeout in milliseconds for locate + action.")]
        [Category("Time")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected EmbeddedIEWindow GetWindow(CodeActivityContext context)
        {
            var window = InputWindow == null ? null : InputWindow.Get(context);
            if (window == null)
                throw new InvalidOperationException("IE Window is required.");
            return window;
        }

        protected IEHtmlElement ResolveTargetElement(CodeActivityContext context)
        {
            var delay = ActivityArgumentHelper.GetOrDefault(DelayBefore, context, 300);
            IeAutomation.ApplyDelay(delay);

            if (TargetType == IeElementTargetType.Element)
            {
                var element = Element == null ? null : Element.Get(context);
                if (element == null)
                    throw new ArgumentException("Element must be provided when Target Type = Element.");
                return element;
            }

            var window = GetWindow(context);
            var locator = IeLocatorHelper.BuildLocator(
                IeLocatorHelper.GetElementJson(context, ElementJson),
                IeLocatorHelper.GetFramePath(context, FramePath));

            var timeout = ActivityArgumentHelper.GetOrDefault(Timeout, context, OperationDefaults.TimeoutMs);
            return window.FindElement(locator, timeout);
        }

        protected IELocator ResolveLocator(CodeActivityContext context) =>
            IeLocatorHelper.BuildLocator(
                IeLocatorHelper.GetElementJson(context, ElementJson),
                IeLocatorHelper.GetFramePath(context, FramePath));

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);

            if (InputWindow == null || InputWindow.Expression == null)
                metadata.AddValidationError("IE Window is required.");

            if (TargetType == IeElementTargetType.Element)
            {
                if (Element == null || Element.Expression == null)
                    metadata.AddValidationError("Element is required when Target Type = Element.");
            }
            else if (ElementJson == null || ElementJson.Expression == null)
            {
                metadata.AddValidationError("Element (Json String) is required when Target Type = Locator.");
            }
        }
    }
}
