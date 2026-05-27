using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore.COM
{
    [DisplayName("Is Checked")]
    [Description("Check selected/checked state of radio/checkbox/select element.")]
    public sealed class IsCheckedActivity : IeElementActivityBase
    {
        public IsCheckedActivity()
        {
            Timeout = 0;
        }

        [Category("Time")]
        [DisplayName("Delay Before")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        [Category("Output")]
        [DisplayName("Value")]
        public OutArgument<bool> Value { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ActivityArgumentHelper.ApplyDelayBefore(DelayBefore, context, 300);

            var result = ResolveWindow(context).is_checked(
                locator: ResolveSelector(context),
                frame_path: ResolveFramePath(context),
                timeout: ResolveTimeout(context));

            Value.Set(context, result);
        }
    }
}
