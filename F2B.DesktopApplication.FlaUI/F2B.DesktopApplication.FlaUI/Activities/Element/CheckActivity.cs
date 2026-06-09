using System.Activities;
using System.ComponentModel;

namespace F2B.DesktopApplication.FlaUI
{
    [DisplayName("Check")]
    [Description("Check the target checkbox or toggle element.")]
    public sealed class CheckActivity : FlaUiElementTargetActivityBase
    {
        protected override void Execute(CodeActivityContext context)
        {
            ResolveTargetElement(context).Check();
        }
    }
}
