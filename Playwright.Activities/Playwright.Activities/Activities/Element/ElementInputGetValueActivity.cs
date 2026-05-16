using System.Activities;
using System.ComponentModel;

namespace Playwright.Activities
{
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    [DisplayName("Element.InputGetValue")]
    public sealed class ElementInputGetValueActivity : ElementTargetActivityBase
    {
        [Category("Output")]
        public OutArgument<string> Value { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Value?.Set(context, ResolveTargetElement(context).InputGetValue());
        }
    }
}
