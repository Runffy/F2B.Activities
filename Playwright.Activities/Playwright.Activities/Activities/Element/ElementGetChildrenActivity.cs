using System.Activities;
using System.ComponentModel;

namespace Playwright.Activities
{
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    [DisplayName("Element.GetChildren")]
    public sealed class ElementGetChildrenActivity : ElementTargetActivityBase
    {
        [RequiredArgument]
        [Category("Input")]
        public InArgument<string> ChildSelector { get; set; }

        [Category("Input")]
        [DefaultValue(false)]
        [TypeConverter("Playwright.Activities.BooleanTypeConverter, Playwright.Activities")]
        public bool Deepdive { get; set; } = false;

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
