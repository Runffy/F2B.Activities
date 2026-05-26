using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Get Children")]
    [Description("Get matching child elements under the target element.")]
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    public sealed class GetChildrenActivity : ElementTargetActivityBase
    {
        public GetChildrenActivity() : base("Get Children") {}

        [DisplayName("Child Selector")]
        [Description("Selector used to filter child elements.")]
        [RequiredArgument]
        [Category("Input.D")]
        public InArgument<string> ChildSelector { get; set; }

        [DisplayName("Deepdive")]
        [Description("Whether to search deeper descendants recursively.")]
        [Category("Input.E")]
        [DefaultValue(false)]
        [TypeConverter("F2B.Browser.Chromium.Playwright.BooleanTypeConverter, F2B.Browser.Chromium.Playwright")]
        public bool Deepdive { get; set; } = false;

        [DisplayName("Children")]
        [Description("Outputs the matched child element collection.")]
        [Category("Output")]
        public OutArgument<PwElement[]> Children { get; set; }

        [DisplayName("Timeout (ms)")]
        [Description("Timeout in milliseconds for locating the target element.")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            var children = ResolveTargetElementWithTimeout(context, Timeout).GetChildren(
                selector: ChildSelector == null ? null : ChildSelector.Get(context),
                deepdive: Deepdive);
            Children?.Set(context, children);
        }
    }
}
