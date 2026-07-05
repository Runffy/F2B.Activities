using System.Activities;
using System.ComponentModel;

namespace F2B.DesktopApplication.FlaUI
{
    [DisplayName("Find Element")]
    [Description("Find the first element matching the selector within the timeout. Retries on transient errors while the UI is still changing.")]
    public sealed class FindElementActivity : FlaUiSelectorActivityBase
    {
        [Category("Input.Z")]
        [DisplayName("Delay Before (ms)")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        [Category("Output")]
        [DisplayName("Element")]
        public OutArgument<UiElement> Element { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ActivityArgumentHelper.ApplyDelayBefore(DelayBefore, context);

            Element.Set(context, CreateClient().FindElement(
                ResolveSelector(context),
                ResolveTimeout(context),
                ResolveInterval(context),
                ResolveInputWindow(context)));
        }
    }
}
