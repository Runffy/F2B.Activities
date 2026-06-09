using System.Activities;
using System.ComponentModel;

namespace F2B.DesktopApplication.FlaUI
{
    [DisplayName("Element Exists")]
    [Description("Instantly check whether an element matching the selector exists.")]
    public sealed class ElementExistsActivity : FlaUiSelectorActivityBase
    {
        public ElementExistsActivity()
        {
            Timeout = 0;
        }

        [Category("Input.Z")]
        [DisplayName("Delay Before (ms)")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        [Category("Output")]
        [DisplayName("Exists")]
        public OutArgument<bool> Exists { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ActivityArgumentHelper.ApplyDelayBefore(DelayBefore, context);
            Exists.Set(context, CreateClient().ElementExists(ResolveSelector(context), ResolveInputWindow(context)));
        }
    }
}
