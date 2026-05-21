using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore
{
    [DisplayName("Get Text")]
    [Description("Get element inner text.")]
    public sealed class GetTextActivity : ElementTargetActivityBase
    {
        [DisplayName("Text")]
        [Category("Output")]
        public OutArgument<string> Text { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var window = GetWindow(context);
            var timeout = ActivityArgumentHelper.GetOrDefault(Timeout, context, OperationDefaults.TimeoutMs);
            string value;

            if (TargetType == IeElementTargetType.Element)
                value = window.GetText(ResolveTargetElement(context), timeout);
            else
                value = window.GetText(ResolveLocator(context), timeout);

            Text?.Set(context, value);
        }
    }
}
