using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore
{
    [DisplayName("Find Elements")]
    [Description("Instantly find all elements matching locator filters (idx ignored). No timeout — use Find Element to wait.")]
    public sealed class FindElementsActivity : CodeActivity
    {
        [DisplayName("IE Window")]
        [Category("Input")]
        public InArgument<EmbeddedIEWindow> InputWindow { get; set; }

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

        [DisplayName("Elements Result")]
        [Category("Output")]
        public OutArgument<IEHtmlElement[]> ElementsResult { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var window = InputWindow == null ? null : InputWindow.Get(context);
            if (window == null)
                throw new InvalidOperationException("IE Window is required.");

            var locator = IeLocatorHelper.BuildLocator(
                IeLocatorHelper.GetSelector(context, Selector),
                IeLocatorHelper.GetFramePath(context, FramePath));

            var parent = ParentObject == null ? null : ParentObject.Get(context);

            IEHtmlElement[] found = parent == null
                ? window.FindElements(locator)
                : window.FindElements(locator, parent);

            ElementsResult?.Set(context, found);
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
