using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore.COM
{
    [DisplayName("Check")]
    [Description("Check target checkbox/radio element.")]
    public sealed class CheckElementActivity : IeElementActivityBase
    {
        [Category("Input")]
        [DisplayName("Trigger Events")]
        [DefaultValue(true)]
        public InArgument<bool> TriggerEvents { get; set; } = true;

        [Category("Time")]
        [DisplayName("Delay Before")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        [Category("Time")]
        [DisplayName("Delay After")]
        [DefaultValue(0)]
        public InArgument<int> DelayAfter { get; set; } = 0;

        protected override void Execute(CodeActivityContext context)
        {
            ResolveWindow(context).check_element(
                locator: ResolveSelector(context),
                frame_path: ResolveFramePath(context),
                timeout: ResolveTimeout(context),
                trigger_events: ActivityArgumentHelper.GetOrDefault(TriggerEvents, context, true),
                delay_before: ActivityArgumentHelper.GetOrDefault(DelayBefore, context, 300),
                delay_after: ActivityArgumentHelper.GetOrDefault(DelayAfter, context, 0));
        }
    }
}
