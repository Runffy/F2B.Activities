using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore.COM
{
    [DisplayName("Get Children")]
    [Description("Get child elements of target element.")]
    public sealed class GetChildrenActivity : IeElementActivityBase
    {
        [Category("Time")]
        [DisplayName("Delay Before")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        [Category("Output")]
        [DisplayName("Elements")]
        public OutArgument<IEWindowController.IEDomElement[]> Elements { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ActivityArgumentHelper.ApplyDelayBefore(DelayBefore, context, 300);

            var children = ResolveWindow(context).get_children(
                locator: ResolveSelector(context),
                frame_path: ResolveFramePath(context),
                timeout: ResolveTimeout(context));

            Elements.Set(context, children);
        }
    }
}
