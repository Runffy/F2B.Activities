using System.Activities;
using System.ComponentModel;

namespace Playwright.Activities
{
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    [DisplayName("Element.GetParent")]
    public sealed class ElementGetParentActivity : ElementTargetActivityBase
    {
        [Category("Input")]
        [DefaultValue(1)]
        public InArgument<int> Level { get; set; } = 1;

        [Category("Output")]
        public OutArgument<PwElement> Parent { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var parent = ResolveTargetElement(context).GetParent(ActivityArgumentHelper.GetOrDefault(Level, context, 1));
            Parent?.Set(context, parent);
        }
    }
}
