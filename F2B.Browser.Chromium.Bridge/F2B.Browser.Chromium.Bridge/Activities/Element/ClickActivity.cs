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

            target.Click(timeoutMs: budget.RemainingMs);
        }
    }
}
