using System.Activities;
using System.ComponentModel;

namespace Playwright.Activities
{
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    [DisplayName("Element.IsChecked")]
    public sealed class ElementIsCheckedActivity : ElementTargetActivityBase
    {
        [Category("Output")]
        public OutArgument<bool> IsChecked { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            IsChecked?.Set(context, ResolveTargetElement(context).IsChecked());
        }
    }
}
