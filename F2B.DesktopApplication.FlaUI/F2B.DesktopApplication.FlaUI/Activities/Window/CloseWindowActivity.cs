using System.Activities;
using System.ComponentModel;

namespace F2B.DesktopApplication.FlaUI
{
    [DisplayName("Close Window")]
    [Description("Close a visible window by <wnd> selector or an existing Window object.")]
    public sealed class CloseWindowActivity : FlaUiWindowTargetActivityBase
    {
        protected override void Execute(CodeActivityContext context)
        {
            ResolveTargetWindow(context).Close();
        }
    }
}
