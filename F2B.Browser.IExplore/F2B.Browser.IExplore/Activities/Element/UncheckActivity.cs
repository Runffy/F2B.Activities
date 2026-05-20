using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore
{
    [DisplayName("Uncheck")]
    [Description("Uncheck a checkbox.")]
    public sealed class UncheckActivity : ElementTargetActivityBase
    {
        protected override void Execute(CodeActivityContext context)
        {
            var window = GetWindow(context);
            var timeout = ActivityArgumentHelper.GetOrDefault(Timeout, context, OperationDefaults.TimeoutMs);

            if (TargetType == IeElementTargetType.Element)
                window.Uncheck(ResolveTargetElement(context), timeout);
            else
                window.Uncheck(ResolveLocator(context), timeout);
        }
    }
}
