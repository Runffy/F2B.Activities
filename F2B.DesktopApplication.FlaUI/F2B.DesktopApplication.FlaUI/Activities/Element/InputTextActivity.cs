using System.Activities;
using System.ComponentModel;

namespace F2B.DesktopApplication.FlaUI
{
    [DisplayName("Input Text")]
    [Description("Enter text into the target desktop element.")]
    public sealed class InputTextActivity : FlaUiElementTargetActivityBase
    {
        [DisplayName("Text")]
        [Category("Input.D")]
        [RequiredArgument]
        public InArgument<string> Text { get; set; }

        [DisplayName("Clear First")]
        [Category("Input.D")]
        [DefaultValue(true)]
        public InArgument<bool> ClearFirst { get; set; } = true;

        protected override void Execute(CodeActivityContext context)
        {
            var text = Text == null ? null : Text.Get(context);
            var clearFirst = ActivityArgumentHelper.GetOrDefault(ClearFirst, context, true);
            ResolveTargetElement(context).InputText(text ?? string.Empty, clearFirst);
        }
    }
}
