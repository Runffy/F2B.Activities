using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore
{
    [DisplayName("Select")]
    [Description("Select option(s). Use F2B.Browser.IExplore.select.text / select.value / select.index in locator JSON.")]
    public sealed class SelectActivity : ElementTargetActivityBase
    {
        protected override void Execute(CodeActivityContext context)
        {
            var window = GetWindow(context);
            var timeout = ActivityArgumentHelper.GetOrDefault(Timeout, context, OperationDefaults.TimeoutMs);

            if (TargetType == IeElementTargetType.Element)
                throw new System.InvalidOperationException("Select requires Target Type = Locator with option keys in the locator JSON.");

            window.Select(ResolveLocator(context), timeout);
        }
    }
}
