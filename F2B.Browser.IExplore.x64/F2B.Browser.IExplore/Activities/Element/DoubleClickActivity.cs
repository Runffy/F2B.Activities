using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore
{
    [DisplayName("Double Click")]
    [Description("Double-click. Optional F2B.Browser.IExplore.click.mode / click.button / click.interval in locator JSON.")]
    public sealed class DoubleClickActivity : ElementTargetActivityBase
    {
        protected override void Execute(CodeActivityContext context)
        {
            var window = GetWindow(context);
            var timeout = ActivityArgumentHelper.GetOrDefault(Timeout, context, OperationDefaults.TimeoutMs);

            if (TargetType == IeElementTargetType.Element)
                window.DoubleClick(ResolveTargetElement(context), timeout);
            else
                window.DoubleClick(ResolveLocator(context), timeout);
        }
    }
}
