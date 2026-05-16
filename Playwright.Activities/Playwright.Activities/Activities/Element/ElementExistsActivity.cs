using System.Activities;
using System.ComponentModel;

namespace Playwright.Activities
{
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    [DisplayName("Element.Exists")]
    public sealed class ElementExistsActivity : ElementTargetActivityBase
    {
        [Category("Output")]
        public OutArgument<bool> Exists { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Exists?.Set(context, ResolveTargetElement(context).Exists());
        }
    }
}
