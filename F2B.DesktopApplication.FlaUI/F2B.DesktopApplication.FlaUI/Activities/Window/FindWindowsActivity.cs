using System.Activities;
using System.ComponentModel;

namespace F2B.DesktopApplication.FlaUI
{
    [DisplayName("Find Windows")]
    [Description("Instantly find all visible windows matching the <wnd> selector XML. Hidden or off-screen windows are ignored.")]
    public sealed class FindWindowsActivity : FlaUiWindowSelectorActivityBase
    {
        public FindWindowsActivity()
        {
            Timeout = 0;
        }

        [Category("Input.Z")]
        [DisplayName("Delay Before (ms)")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        [Category("Output")]
        [DisplayName("Windows")]
        public OutArgument<UiWindow[]> Windows { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ActivityArgumentHelper.ApplyDelayBefore(DelayBefore, context);
            Windows.Set(context, new DesktopAutomationClient().FindWindows(ResolveSelector(context)));
        }
    }
}
