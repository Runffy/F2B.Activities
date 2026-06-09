using System.Activities;
using System.ComponentModel;
using System.Linq;

namespace F2B.DesktopApplication.FlaUI
{
    [DisplayName("Find Elements")]
    [Description("Instantly find all elements matching the selector.")]
    public sealed class FindElementsActivity : FlaUiSelectorActivityBase
    {
        public FindElementsActivity()
        {
            Timeout = 0;
        }

        [Category("Input.Z")]
        [DisplayName("Delay Before (ms)")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        [Category("Output")]
        [DisplayName("Elements")]
        public OutArgument<UiElement[]> Elements { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ActivityArgumentHelper.ApplyDelayBefore(DelayBefore, context);

            var elements = CreateClient().FindElements(
                ResolveSelector(context),
                maxResults: int.MaxValue,
                timeoutMilliseconds: 0,
                intervalMilliseconds: ResolveInterval(context),
                inputWindow: ResolveInputWindow(context));

            Elements.Set(context, elements.ToArray());
        }
    }
}
