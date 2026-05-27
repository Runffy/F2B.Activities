using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore.COM
{
    [DisplayName("Get Text")]
    [Description("Get text/value of target element.")]
    public sealed class GetElementTextActivity : IeElementActivityBase
    {
        [Category("Time")]
        [DisplayName("Delay Before")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        [Category("Output")]
        [DisplayName("Text")]
        public OutArgument<string> Text { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ActivityArgumentHelper.ApplyDelayBefore(DelayBefore, context, 300);

            var result = ResolveWindow(context).get_element_text(
                locator: ResolveSelector(context),
                frame_path: ResolveFramePath(context),
                timeout: ResolveTimeout(context));

            Text.Set(context, result);
        }
    }
}
