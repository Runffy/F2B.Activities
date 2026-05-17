using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Check")]
    [Description("Check the target element.")]
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    public sealed class CheckActivity : ElementTargetActivityBase
    {
        protected override void Execute(CodeActivityContext context)
        {
            ResolveTargetElement(context).Check();
        }
    }
}
