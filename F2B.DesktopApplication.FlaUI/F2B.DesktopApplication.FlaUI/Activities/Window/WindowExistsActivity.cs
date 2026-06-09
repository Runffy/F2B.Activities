using System.Activities;
using System.ComponentModel;

namespace F2B.DesktopApplication.FlaUI
{
    [DisplayName("Window Exists")]
    [Description("Instantly check whether a window matching the <wnd> selector exists.")]
    public sealed class WindowExistsActivity : FlaUiWindowSelectorActivityBase
    {
        public WindowExistsActivity()
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
            Exists.Set(context, new DesktopAutomationClient().WindowExists(ResolveSelector(context)));
        }
    }
}
