using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Double Click")]
    [Description("Double-click the target element.")]
    [TypeDescriptionProvider(typeof(BridgeElementTargetTypeDescriptionProvider))]
    public sealed class DoubleClickActivity : BridgeElementTargetActivityBase
    {
        public DoubleClickActivity() : base("Double Click") { }

        [DisplayName("Click Method")]
        [Description("Javascript uses element.click() or dblclick dispatch. ClickEvent dispatches mouse events at the element center.")]
        [Category("Input.D")]
        [DefaultValue(BridgeClickMethod.Javascript)]
        [TypeConverter("F2B.Browser.Chromium.Bridge.BridgeClickMethodTypeConverter, F2B.Browser.Chromium.Bridge")]
        public BridgeClickMethod ClickMethod { get; set; } = BridgeClickMethod.Javascript;

        [DisplayName("Timeout (ms)")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            var budget = new TimeoutBudget(BridgeActivityArgumentHelper.GetOrDefault(Timeout, context, 15000));
            var target = ResolveTargetElement(context, budget.RemainingMs);
            if (budget.RemainingMs <= 0)
                throw new TimeoutException("DoubleClick timeout before operation.");
            target.DoubleClick(clickMethod: ClickMethod, timeoutMs: budget.RemainingMs);
        }
    }
}
