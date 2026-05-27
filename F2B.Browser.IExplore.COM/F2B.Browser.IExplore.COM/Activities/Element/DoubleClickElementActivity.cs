using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore.COM
{
    [DisplayName("Double Click")]
    [Description("Perform double click on target element.")]
    public sealed class DoubleClickElementActivity : IeElementActivityBase
    {
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
            ResolveWindow(context).double_click_element(
                locator: ResolveSelector(context),
                frame_path: ResolveFramePath(context),
                timeout: ResolveTimeout(context),
                delay_before: ActivityArgumentHelper.GetOrDefault(DelayBefore, context, 300),
                delay_after: ActivityArgumentHelper.GetOrDefault(DelayAfter, context, 0));
        }
    }
}
