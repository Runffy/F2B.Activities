using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore
{
    [DisplayName("Get Value")]
    [Description("Get element value attribute.")]
    public sealed class GetValueActivity : ElementTargetActivityBase
    {
        [DisplayName("Value")]
        [Category("Output")]
        public OutArgument<string> Value { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var window = GetWindow(context);
            var timeout = ActivityArgumentHelper.GetOrDefault(Timeout, context, OperationDefaults.TimeoutMs);
            string result;

            if (TargetType == IeElementTargetType.Element)
                result = window.GetValue(ResolveTargetElement(context), timeout);
            else
                result = window.GetValue(ResolveLocator(context), timeout);

            Value?.Set(context, result);
        }
    }
}
