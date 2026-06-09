using System.Activities;
using System.ComponentModel;

namespace F2B.DesktopApplication.FlaUI
{
    [DisplayName("Find Window")]
    [Description("Find a window using <wnd> selector XML within the timeout.")]
    public sealed class FindWindowActivity : FlaUiWindowSelectorActivityBase
    {
        [Category("Input.Z")]
        [DisplayName("Delay Before (ms)")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        [Category("Output")]
        [DisplayName("Window")]
        public OutArgument<UiWindow> Window { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ActivityArgumentHelper.ApplyDelayBefore(DelayBefore, context);

            var client = new DesktopAutomationClient();
            Window.Set(context, client.FindWindow(ResolveSelector(context), ResolveTimeout(context), ResolveInterval(context)));
        }
    }
}
