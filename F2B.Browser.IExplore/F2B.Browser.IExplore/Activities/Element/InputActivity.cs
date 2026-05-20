using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore
{
    [DisplayName("Input")]
    [Description("Type into an element. Use 'value' in locator JSON, or set Value when using an element handle.")]
    public sealed class InputActivity : ElementTargetActivityBase
    {
        [DisplayName("Value")]
        [Description("Text to type when Target Type = Element.")]
        [Category("Input")]
        public InArgument<string> Value { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var window = GetWindow(context);
            var timeout = ActivityArgumentHelper.GetOrDefault(Timeout, context, OperationDefaults.TimeoutMs);

            if (TargetType == IeElementTargetType.Element)
            {
                var element = ResolveTargetElement(context);
                var value = Value == null ? null : Value.Get(context);
                window.Input(element, value ?? string.Empty, timeout);
            }
            else
            {
                window.Input(ResolveLocator(context), timeout);
            }
        }
    }
}
