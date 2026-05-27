using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore.COM
{
    [DisplayName("Wait For Window Ready")]
    [Description("Wait until current IE document becomes interactive/complete.")]
    public sealed class WaitReadyActivity : IeWindowActivityBase
    {
        [Category("Time")]
        [DisplayName("Delay Before")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        [Category("Time")]
        [DisplayName("Timeout")]
        [DefaultValue(60000)]
        public InArgument<int> Timeout { get; set; } = 60000;

        [Category("Time")]
        [DisplayName("Interval")]
        [DefaultValue(200)]
        public InArgument<int> Interval { get; set; } = 200;

        [Category("Output")]
        [DisplayName("Window")]
        public OutArgument<IEWindowController> Window { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ActivityArgumentHelper.ApplyDelayBefore(DelayBefore, context, 300);

            var result = ResolveWindow(context).wait_ready(
                timeout: ActivityArgumentHelper.GetOrDefault(Timeout, context, 60000),
                interval: ActivityArgumentHelper.GetOrDefault(Interval, context, 200));

            Window.Set(context, result);
        }
    }
}
