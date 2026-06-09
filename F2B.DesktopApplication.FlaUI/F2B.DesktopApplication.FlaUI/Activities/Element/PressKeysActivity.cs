using System.Activities;
using System.ComponentModel;

namespace F2B.DesktopApplication.FlaUI
{
    [DisplayName("Press Keys")]
    [Description("Send keystrokes to the target desktop element.")]
    public sealed class PressKeysActivity : FlaUiElementTargetActivityBase
    {
        [DisplayName("Keys")]
        [Category("Input.D")]
        [RequiredArgument]
        public InArgument<string> Keys { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var keys = Keys == null ? null : Keys.Get(context);
            ResolveTargetElement(context).PressKeys(keys);
        }
    }
}
