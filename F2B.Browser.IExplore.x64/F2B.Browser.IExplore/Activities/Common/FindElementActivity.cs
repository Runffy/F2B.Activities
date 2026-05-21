using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore
{
    [DisplayName("Find Element")]
    [Description("Find a single element by JSON locator.")]
    [Designer(typeof(IeFindElementActivityDesigner))]
    public sealed class FindElementActivity : CodeActivity
    {
        [DisplayName("IE Window")]
        [Category("Input")]
        public InArgument<EmbeddedIEWindow> InputWindow { get; set; }

        [DisplayName("Base On")]
        [Description("Search from the window document or under a parent element.")]
        [Category("Input")]
        [DefaultValue(IeBaseOn.Window)]
        public IeBaseOn BaseOn { get; set; } = IeBaseOn.Window;

        [DisplayName("Parent Object")]
        [Description("Parent element for scoped search; empty uses the document.")]
        [Category("Input")]
        public InArgument<IEHtmlElement> ParentObject { get; set; }

        [DisplayName("Selector (Json String)")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<string> Selector { get; set; }

        [DisplayName("Frame (Json String)")]
        [Category("Input")]
        public InArgument<string> FramePath { get; set; }

        [DisplayName("Timeout (ms)")]
        [Category("Input")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        [DisplayName("Delay Before")]
        [Category("Input")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        [DisplayName("Element Result")]
        [Category("Output")]
        public OutArgument<IEHtmlElement> ElementResult { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var window = InputWindow == null ? null : InputWindow.Get(context);
            if (window == null)
                throw new InvalidOperationException("IE Window is required.");

            IeAutomation.ApplyDelay(ActivityArgumentHelper.GetOrDefault(DelayBefore, context, 300));

            var locator = IeLocatorHelper.BuildLocator(
                IeLocatorHelper.GetSelector(context, Selector),
                IeLocatorHelper.GetFramePath(context, FramePath));

            var timeout = ActivityArgumentHelper.GetOrDefault(Timeout, context, OperationDefaults.TimeoutMs);
            IEHtmlElement found;

            if (BaseOn == IeBaseOn.Element)
            {
                var parent = ParentObject == null ? null : ParentObject.Get(context);
                found = parent == null
                    ? window.FindElement(locator, timeout)
                    : window.FindElement(locator, parent, timeout);
            }
            else
            {
                found = window.FindElement(locator, timeout);
            }

            ElementResult?.Set(context, found);
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);
            if (InputWindow == null || InputWindow.Expression == null)
                metadata.AddValidationError("IE Window is required.");
            if (Selector == null || Selector.Expression == null)
                metadata.AddValidationError("Selector (Json String) is required.");
        }
    }
}
