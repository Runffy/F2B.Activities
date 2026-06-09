using System.Activities;
using System.ComponentModel;

namespace F2B.DesktopApplication.FlaUI
{
    [DisplayName("Uncheck")]
    [Description("Uncheck the target checkbox or toggle element.")]
    public sealed class UncheckActivity : FlaUiElementTargetActivityBase
    {
        protected override void Execute(CodeActivityContext context)
        {
            ResolveTargetElement(context).Uncheck();
        }
    }
}
