using System.Activities;
using System.ComponentModel;

namespace Playwright.Activities
{
    [Designer(typeof(CanvasFieldsActivityDesigner))]
    [DisplayName("Element.FindElement")]
    public sealed class ElementFindElementActivity : CodeActivity
    {
        [RequiredArgument]
        [Category("Input")]
        public InArgument<PwElement> Element { get; set; }

        [RequiredArgument]
        [Category("Input")]
        public InArgument<string> Selector { get; set; }

        [Category("Input")]
        [DefaultValue(0)]
        public InArgument<int> Index { get; set; } = 0;

        [Category("Input")]
        [DefaultValue(15000)]
        public InArgument<int?> Timeout { get; set; } = 15000;

        [Category("Input")]
        [DefaultValue(FindElementWaitState.None)]
        public FindElementWaitState WaitState { get; set; } = FindElementWaitState.None;

        [Category("Input")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        [Category("Output")]
        public OutArgument<PwElement> FoundElement { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var found = Element.Get(context).FindElement(
                selector: Selector.Get(context),
                index: ActivityArgumentHelper.GetOrDefault(Index, context, 0),
                timeout: Timeout == null ? null : (double?)Timeout.Get(context),
                waitState: ActivityArgumentHelper.ToWaitStateString(WaitState),
                delayBefore: ActivityArgumentHelper.GetOrDefault(DelayBefore, context, 300));
            FoundElement?.Set(context, found);
        }
    }
}
