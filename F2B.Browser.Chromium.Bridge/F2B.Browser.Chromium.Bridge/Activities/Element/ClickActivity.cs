using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Click")]
    [Description("Click the target element.")]
    [TypeDescriptionProvider(typeof(BridgeElementTargetTypeDescriptionProvider))]
    public sealed class ClickActivity : BridgeElementTargetActivityBase
    {
        public ClickActivity() : base("Click")
        {
        }

        [DisplayName("Click Method")]
        [Description("Javascript uses element.click(). ClickEvent dispatches mouse events at the element center.")]
        [Category("Input.D")]
        [DefaultValue(BridgeClickMethod.Javascript)]
        [TypeConverter("F2B.Browser.Chromium.Bridge.BridgeClickMethodTypeConverter, F2B.Browser.Chromium.Bridge")]
        public BridgeClickMethod ClickMethod { get; set; } = BridgeClickMethod.Javascript;

        [DisplayName("Timeout (ms)")]
        [Description("Timeout in milliseconds for locating the target element and click.")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            var totalTimeout = BridgeActivityArgumentHelper.GetOrDefault(Timeout, context, 15000);
            var budget = new TimeoutBudget(totalTimeout);
            var target = ResolveTargetElement(context, budget.RemainingMs);
            if (budget.RemainingMs <= 0)
                throw new TimeoutException("Click timeout before operation.");

            target.Click(clickMethod: ClickMethod, timeoutMs: budget.RemainingMs);
        }
    }
}
