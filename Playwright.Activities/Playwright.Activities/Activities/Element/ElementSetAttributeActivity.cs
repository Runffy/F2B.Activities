using System.Activities;
using System.ComponentModel;

namespace Playwright.Activities
{
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    [DisplayName("Element.SetAttribute")]
    public sealed class ElementSetAttributeActivity : ElementTargetActivityBase
    {
        [RequiredArgument]
        [Category("Input")]
        public InArgument<string> Name { get; set; }

        [RequiredArgument]
        [Category("Input")]
        public InArgument<string> Value { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ResolveTargetElement(context).SetAttribute(
                name: Name.Get(context),
                value: Value == null ? null : Value.Get(context));
        }
    }
}
