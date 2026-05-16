using System.Activities;
using System.ComponentModel;

namespace Playwright.Activities
{
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    [DisplayName("Element.Uncheck")]
    public sealed class ElementUncheckActivity : ElementTargetActivityBase
    {
        protected override void Execute(CodeActivityContext context)
        {
            ResolveTargetElement(context).Uncheck();
        }
    }
}
