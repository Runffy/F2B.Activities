using System.Activities;
using System.ComponentModel;

namespace Playwright.Activities
{
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    [DisplayName("Element.GetText")]
    public sealed class ElementGetTextActivity : ElementTargetActivityBase
    {
        [Category("Output")]
        public OutArgument<string> Text { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Text?.Set(context, ResolveTargetElement(context).GetText());
        }
    }
}
