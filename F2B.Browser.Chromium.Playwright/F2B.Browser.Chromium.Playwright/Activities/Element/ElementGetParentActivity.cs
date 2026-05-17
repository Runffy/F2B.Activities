using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Get Element Parent")]
    [Description("Get a parent element at the specified level.")]
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    public sealed class ElementGetParentActivity : ElementTargetActivityBase
    {
        [DisplayName("Level")]
        [Description("Number of levels to move up.")]
        [Category("Input")]
        [DefaultValue(1)]
        public InArgument<int> Level { get; set; } = 1;

        [DisplayName("Parent")]
        [Description("Outputs the located parent element.")]
        [Category("Output")]
        public OutArgument<PwElement> Parent { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var parent = ResolveTargetElement(context).GetParent(ActivityArgumentHelper.GetOrDefault(Level, context, 1));
            Parent?.Set(context, parent);
        }
    }
}
