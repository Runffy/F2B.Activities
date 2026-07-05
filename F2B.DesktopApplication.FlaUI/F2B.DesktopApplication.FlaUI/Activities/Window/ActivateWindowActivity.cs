using System.Activities;
using System.ComponentModel;

namespace F2B.DesktopApplication.FlaUI
{
    [DisplayName("Activate Window")]
    [Description("Activate a visible window by <wnd> selector or an existing Window object.")]
    public sealed class ActivateWindowActivity : FlaUiWindowTargetActivityBase
    {
        protected override void Execute(CodeActivityContext context)
        {
            ResolveTargetWindow(context).Activate();
        }
    }
}
