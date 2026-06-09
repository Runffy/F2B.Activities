using System.Activities;
using System.ComponentModel;

namespace F2B.DesktopApplication.FlaUI
{
    [DisplayName("Get Text")]
    [Description("Read text from the target desktop element.")]
    public sealed class GetTextActivity : FlaUiElementTargetActivityBase
    {
        [Category("Output")]
        [DisplayName("Text")]
        public OutArgument<string> Text { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Text.Set(context, ResolveTargetElement(context).GetText());
        }
    }
}
