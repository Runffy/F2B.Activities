using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore
{
    [DisplayName("Input")]
    [Description("Type into an element. Set Value, or include F2B.Browser.IExplore.value in element JSON when Target Type = Locator.")]
    public sealed class InputActivity : ElementTargetActivityBase
    {
        [DisplayName("Value")]
        [Description("Text to type. Used for both Locator and Element target types.")]
        [Category("Input")]
        public InArgument<string> Value { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var window = GetWindow(context);
            var timeout = ActivityArgumentHelper.GetOrDefault(Timeout, context, OperationDefaults.TimeoutMs);
            var hasValue = Value != null && Value.Expression != null;
            var text = hasValue ? (Value.Get(context) ?? string.Empty) : null;

            if (TargetType == IeElementTargetType.Element)
            {
                var element = ResolveTargetElement(context);
                window.Input(element, text ?? string.Empty, timeout);
            }
            else if (hasValue)
            {
                window.Input(ResolveLocator(context), text, timeout);
            }
            else
            {
                window.Input(ResolveLocator(context), timeout);
            }
        }
    }
}
