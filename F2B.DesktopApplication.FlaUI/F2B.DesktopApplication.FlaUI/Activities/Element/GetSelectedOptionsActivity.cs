using System.Activities;
using System.ComponentModel;

namespace F2B.DesktopApplication.FlaUI
{
    [DisplayName("Get Selected Options")]
    [Description("Get selected option texts from combo box, list box, or selection pattern.")]
    public sealed class GetSelectedOptionsActivity : FlaUiElementTargetActivityBase
    {
        [Category("Output")]
        [DisplayName("Options")]
        [Description("All selected option texts.")]
        public OutArgument<string[]> Options { get; set; }

        [Category("Output")]
        [DisplayName("Selected Text")]
        [Description("First selected option text. Optional shortcut when a single String variable is enough.")]
        public OutArgument<string> SelectedText { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var options = ResolveTargetElement(context).GetSelectedOptions() ?? new string[0];

            if (Options != null)
                Options.Set(context, options);

            if (SelectedText != null)
                SelectedText.Set(context, options.Length > 0 ? options[0] : string.Empty);
        }
    }
}
