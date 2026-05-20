using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore
{
    [DisplayName("Click")]
    [Description("Click an element (synthetic or physical per locator JSON: mode, button).")]
    public sealed class ClickActivity : ElementTargetActivityBase
    {
        protected override void Execute(CodeActivityContext context)
        {
            var window = GetWindow(context);
            var timeout = ActivityArgumentHelper.GetOrDefault(Timeout, context, OperationDefaults.TimeoutMs);

            if (TargetType == IeElementTargetType.Element)
                window.Click(ResolveTargetElement(context), timeout);
            else
                window.Click(ResolveLocator(context), timeout);
        }
    }
}
