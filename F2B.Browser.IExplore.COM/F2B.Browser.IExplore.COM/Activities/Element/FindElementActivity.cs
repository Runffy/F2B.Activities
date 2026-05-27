using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore.COM
{
    [DisplayName("Find Element")]
    [Description("Find first matching element.")]
    public sealed class FindElementActivity : IeElementActivityBase
    {
        [Category("Time")]
        [DisplayName("Delay Before")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        [Category("Time")]
        [DisplayName("Interval")]
        [DefaultValue(200)]
        public InArgument<int> Interval { get; set; } = 200;

        [Category("Output")]
        [DisplayName("Element")]
        public OutArgument<IEWindowController.IEDomElement> Element { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ActivityArgumentHelper.ApplyDelayBefore(DelayBefore, context, 300);

            var element = ResolveWindow(context).find_element(
                locator: ResolveSelector(context),
                frame_path: ResolveFramePath(context),
                timeout: ResolveTimeout(context),
                interval: ActivityArgumentHelper.GetOrDefault(Interval, context, 200));

            Element.Set(context, element);
        }
    }
}
