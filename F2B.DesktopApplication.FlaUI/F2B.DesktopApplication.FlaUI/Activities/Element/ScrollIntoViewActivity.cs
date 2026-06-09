using System.Activities;
using System.ComponentModel;

namespace F2B.DesktopApplication.FlaUI
{
    [DisplayName("Scroll Into View")]
    [Description("Scroll the target element into view.")]
    public sealed class ScrollIntoViewActivity : FlaUiElementTargetActivityBase
    {
        protected override void Execute(CodeActivityContext context)
        {
            ResolveTargetElement(context).ScrollIntoView();
        }
    }
}
