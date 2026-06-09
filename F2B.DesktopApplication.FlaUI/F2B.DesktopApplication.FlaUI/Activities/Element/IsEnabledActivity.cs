using System.Activities;
using System.ComponentModel;

namespace F2B.DesktopApplication.FlaUI
{
    [DisplayName("Is Enabled")]
    [Description("Check whether the target desktop element is enabled.")]
    public sealed class IsEnabledActivity : FlaUiElementTargetActivityBase
    {
        [Category("Output")]
        [DisplayName("Is Enabled")]
        public OutArgument<bool> Enabled { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Enabled.Set(context, ResolveTargetElement(context).IsEnabled());
        }
    }
}
