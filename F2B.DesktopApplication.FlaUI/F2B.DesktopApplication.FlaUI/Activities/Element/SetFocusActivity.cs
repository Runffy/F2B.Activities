using System.Activities;
using System.ComponentModel;

namespace F2B.DesktopApplication.FlaUI
{
    [DisplayName("Set Focus")]
    [Description("Set keyboard focus to the target desktop element.")]
    public sealed class SetFocusActivity : FlaUiElementTargetActivityBase
    {
        protected override void Execute(CodeActivityContext context)
        {
            ResolveTargetElement(context).Focus();
        }
    }
}
