using System.Activities;
using System.ComponentModel;

namespace Playwright.Activities
{
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    [DisplayName("Element.GetAttribute")]
    public sealed class ElementGetAttributeActivity : ElementTargetActivityBase
    {
        [RequiredArgument]
        [Category("Input")]
        public InArgument<string> Name { get; set; }

        [Category("Output")]
        public OutArgument<string> Value { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Value?.Set(context, ResolveTargetElement(context).GetAttribute(Name.Get(context)));
        }
    }
}
