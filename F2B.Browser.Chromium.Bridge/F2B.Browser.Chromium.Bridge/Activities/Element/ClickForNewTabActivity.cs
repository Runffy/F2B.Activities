using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Click For New Tab")]
    [Description("Click an element and wait for a new tab.")]
    [TypeDescriptionProvider(typeof(BridgeElementTargetTypeDescriptionProvider))]
    public sealed class ClickForNewTabActivity : BridgeElementTargetActivityBase
    {
        public ClickForNewTabActivity() : base("Click For New Tab") { }

        [DisplayName("Timeout (ms)")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        [DisplayName("Tab")]
        [Category("Output")]
        public OutArgument<BwTab> Tab { get; set; }

        [DisplayName("Tab Info")]
        [Category("Output")]
        public OutArgument<BwTabInfo> TabInfo { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var budget = new TimeoutBudget(BridgeActivityArgumentHelper.GetOrDefault(Timeout, context, 15000));
            var target = ResolveTargetElement(context, budget.RemainingMs);
            if (budget.RemainingMs <= 0)
                throw new TimeoutException("ClickForNewTab timeout before operation.");
            var tab = target.ClickForNewTab(budget.RemainingMs);
            Tab?.Set(context, tab);
            TabInfo?.Set(context, tab.GetInfo());
        }
    }
}
