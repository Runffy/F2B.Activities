using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore.COM
{
    [DisplayName("Find Elements")]
    [Description("Find all matching elements.")]
    public sealed class FindElementsActivity : IeElementActivityBase
    {
        public FindElementsActivity()
        {
            Timeout = 0;
        }

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

            var elements = ResolveWindow(context).find_elements(
                locator: ResolveSelector(context),
                frame_path: ResolveFramePath(context));

            Elements.Set(context, elements);
        }
    }
}
