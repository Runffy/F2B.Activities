using System.Activities;
using System.ComponentModel;

namespace Playwright.Activities
{
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    [DisplayName("Element.GetRect")]
    public sealed class ElementGetRectActivity : ElementTargetActivityBase
    {
        [Category("Output")]
        public OutArgument<ElementRect> Rect { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Rect?.Set(context, ResolveTargetElement(context).GetRect());
        }
    }
}
