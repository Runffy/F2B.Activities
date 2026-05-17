using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Get Element Checked State")]
    [Description("Determine whether the target element is checked.")]
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    public sealed class ElementIsCheckedActivity : ElementTargetActivityBase
    {
        [DisplayName("Checked State")]
        [Description("Outputs whether the element is checked.")]
        [Category("Output")]
        public OutArgument<bool> IsChecked { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            IsChecked?.Set(context, ResolveTargetElement(context).IsChecked());
        }
    }
}
