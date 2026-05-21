using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore
{
    [DisplayName("Check")]
    [Description("Check a checkbox or radio.")]
    public sealed class CheckActivity : ElementTargetActivityBase
    {
        protected override void Execute(CodeActivityContext context)
        {
            var window = GetWindow(context);
            var timeout = ActivityArgumentHelper.GetOrDefault(Timeout, context, OperationDefaults.TimeoutMs);

            if (TargetType == IeElementTargetType.Element)
                window.Check(ResolveTargetElement(context), timeout);
            else
                window.Check(ResolveLocator(context), timeout);
        }
    }
}
