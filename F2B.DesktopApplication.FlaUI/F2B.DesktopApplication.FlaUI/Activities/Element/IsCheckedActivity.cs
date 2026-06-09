using System.Activities;
using System.ComponentModel;

namespace F2B.DesktopApplication.FlaUI
{
    [DisplayName("Is Checked")]
    [Description("Check whether the target checkbox or toggle element is checked.")]
    public sealed class IsCheckedActivity : FlaUiElementTargetActivityBase
    {
        [Category("Output")]
        [DisplayName("Is Checked")]
        public OutArgument<bool> Checked { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Checked.Set(context, ResolveTargetElement(context).IsChecked());
        }
    }
}
