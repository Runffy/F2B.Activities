using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Element Locator")]
    [Description("Instantly create a sync PwLocator from PwTab or PwElement. Does not wait for elements; use Count to check matches.")]
    [Designer(typeof(ParentSelectorActivityDesigner))]
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

        [DisplayName("Locator Result")]
        [Description("Outputs sync locator wrapper.")]
        [Category("Output")]
        public OutArgument<PwLocator> LocatorResult { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var parent = ParentObject == null ? null : ParentObject.Get(context);
            var selector = (Selector == null ? null : Selector.Get(context)) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(selector))
            {
                throw new InvalidOperationException("Selector is required.");
            }

            PwLocator locator;
            if (parent is PwTab tab)
            {
                locator = tab.Locate(selector, timeout: null, delayBefore: 0);
            }
            else if (parent is PwElement element)
            {
                locator = element.Locate(selector, timeout: null, delayBefore: 0);
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
