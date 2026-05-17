using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Get Element Children")]
    [Description("Get matching child elements under the target element.")]
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    public sealed class ElementGetChildrenActivity : ElementTargetActivityBase
    {
        [DisplayName("Child Selector")]
        [Description("Selector used to filter child elements.")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<string> ChildSelector { get; set; }

        [DisplayName("Deepdive")]
        [Description("Whether to search deeper descendants recursively.")]
        [Category("Input")]
        [DefaultValue(false)]
        [TypeConverter("F2B.Browser.Chromium.Playwright.BooleanTypeConverter, F2B.Browser.Chromium.Playwright")]
        public bool Deepdive { get; set; } = false;

        [DisplayName("Children")]
        [Description("Outputs the matched child element collection.")]
        [Category("Output")]
        public OutArgument<PwElement[]> Children { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var children = ResolveTargetElement(context).GetChildren(
                selector: ChildSelector == null ? null : ChildSelector.Get(context),
                deepdive: Deepdive);
            Children?.Set(context, children);
        }
    }
}
