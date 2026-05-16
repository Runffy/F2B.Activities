using System.Activities;
using System.ComponentModel;

namespace Playwright.Activities
{
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    [DisplayName("Element.Check")]
    public sealed class ElementCheckActivity : ElementTargetActivityBase
    {
        protected override void Execute(CodeActivityContext context)
        {
            ResolveTargetElement(context).Check();
        }
    }
}
