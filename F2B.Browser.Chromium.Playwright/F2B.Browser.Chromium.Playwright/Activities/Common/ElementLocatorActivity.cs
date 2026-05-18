using System;
using System.Activities;
using System.ComponentModel;
using Microsoft.Playwright;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Element Locator")]
    [Description("Locate a selector from PwTab or PwElement and return native Playwright ILocator.")]
    public sealed class ElementLocatorActivity : CodeActivity
    {
        [DisplayName("Parent Object")]
        [Description("Root object for query. Accepts PwTab or PwElement.")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<object> ParentObject { get; set; }

        [DisplayName("Selector")]
        [Description("Selector used to locate the target.")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<string> Selector { get; set; }

        [DisplayName("Timeout")]
        [Description("Timeout in milliseconds. Used to wait for locator attached state.")]
        [Category("Input")]
        [DefaultValue(15000)]
        public InArgument<int?> Timeout { get; set; } = 15000;

        [DisplayName("Delay Before")]
        [Description("Wait time in milliseconds before locating.")]
        [Category("Input")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        [DisplayName("Locator Result")]
        [Description("Outputs native Playwright locator.")]
        [Category("Output")]
        public OutArgument<ILocator> LocatorResult { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var parent = ParentObject == null ? null : ParentObject.Get(context);
            var selector = (Selector == null ? null : Selector.Get(context)) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(selector))
            {
                throw new InvalidOperationException("Selector is required.");
            }

            var timeoutValue = ActivityArgumentHelper.GetOrDefault(Timeout, context, 15000);
            var timeoutMs = timeoutValue ?? 15000;
            if (timeoutMs < 0)
            {
                timeoutMs = 0;
            }

            var delayBefore = ActivityArgumentHelper.GetOrDefault(DelayBefore, context, 300);
            if (delayBefore < 0)
            {
                delayBefore = 0;
            }

            ILocator locator;
            if (parent is PwTab tab)
            {
                locator = tab.Locate(selector, timeoutMs, delayBefore);
            }
            else if (parent is PwElement element)
            {
                locator = element.Locate(selector, timeoutMs, delayBefore);
            }
            else
            {
                throw new InvalidOperationException("ParentObject must be PwTab or PwElement.");
            }

            LocatorResult?.Set(context, locator);
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);

            if (ParentObject == null || ParentObject.Expression == null)
            {
                metadata.AddValidationError("ParentObject is required.");
            }

            if (Selector == null || Selector.Expression == null)
            {
                metadata.AddValidationError("Selector is required.");
            }
        }
    }
}
