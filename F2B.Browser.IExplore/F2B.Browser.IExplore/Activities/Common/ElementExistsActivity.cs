using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore
{
    [DisplayName("Element Exists")]
    [Description("Instantly check whether an element exists; returns true or false without waiting.")]
    [Designer(typeof(IeFindElementActivityDesigner))]
    public sealed class ElementExistsActivity : CodeActivity
    {
        [DisplayName("IE Window")]
        [Category("Input")]
        public InArgument<EmbeddedIEWindow> InputWindow { get; set; }

        [DisplayName("Base On")]
        [Description("Check in the window document or under a parent element.")]
        [Category("Input")]
        [DefaultValue(IeBaseOn.Window)]
        public IeBaseOn BaseOn { get; set; } = IeBaseOn.Window;

        [DisplayName("Parent Object")]
        [Description("Parent element for scoped check; empty uses the document.")]
        [Category("Input")]
        public InArgument<IEHtmlElement> ParentObject { get; set; }

        [DisplayName("Selector (Json String)")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<string> Selector { get; set; }

        [DisplayName("Frame (Json String)")]
        [Category("Input")]
        public InArgument<string> FramePath { get; set; }

        [DisplayName("Exists")]
        [Category("Output")]
        public OutArgument<bool> Exists { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var window = InputWindow == null ? null : InputWindow.Get(context);
            if (window == null)
                throw new InvalidOperationException("IE Window is required.");

            var locator = IeLocatorHelper.BuildLocator(
                IeLocatorHelper.GetSelector(context, Selector),
                IeLocatorHelper.GetFramePath(context, FramePath));

            bool exists;
            if (BaseOn == IeBaseOn.Element)
            {
                var parent = ParentObject == null ? null : ParentObject.Get(context);
                exists = window.ElementExists(locator, parent);
            }
            else
            {
                exists = window.ElementExists(locator);
            }

            Exists?.Set(context, exists);
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
