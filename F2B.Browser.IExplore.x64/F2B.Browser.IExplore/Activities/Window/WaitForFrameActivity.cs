using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore
{
    [DisplayName("Wait Frame")]
    [Description("Wait until frame path from locator exists.")]
    public sealed class WaitForFrameActivity : CodeActivity
    {
        [DisplayName("IE Window")]
        [Category("Input")]
        public InArgument<EmbeddedIEWindow> InputWindow { get; set; }

        [DisplayName("Element (Json String)")]
        [Description("Element locator; frame path is used; element filters optional.")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<string> ElementJson { get; set; }

        [DisplayName("Frame (Json String)")]
        [Category("Input")]
        public InArgument<string> FramePath { get; set; }

        [DisplayName("Timeout (ms)")]
        [Category("Input")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            var window = InputWindow == null ? null : InputWindow.Get(context);
            if (window == null)
                throw new InvalidOperationException("IE Window is required.");

            var locator = IeLocatorHelper.BuildLocator(
                IeLocatorHelper.GetElementJson(context, ElementJson),
                IeLocatorHelper.GetFramePath(context, FramePath));

            var timeout = ActivityArgumentHelper.GetOrDefault(Timeout, context, OperationDefaults.TimeoutMs);
            window.WaitForFrame(locator, timeout);
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);
            if (InputWindow == null || InputWindow.Expression == null)
                metadata.AddValidationError("IE Window is required.");
            if (ElementJson == null || ElementJson.Expression == null)
                metadata.AddValidationError("Element (Json String) is required.");
        }
    }
}
