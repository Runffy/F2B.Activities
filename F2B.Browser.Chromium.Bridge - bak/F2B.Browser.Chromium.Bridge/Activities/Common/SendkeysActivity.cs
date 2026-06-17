using System;
using System.Activities;
using System.ComponentModel;
using System.Activities.Presentation;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Sendkeys")]
    [Description("Send keyboard keys to a tab or element.")]
    [Designer(typeof(BridgeCanvasFieldsActivityDesigner))]
    public sealed class SendkeysActivity : CodeActivity
    {
        public SendkeysActivity()
        {
            DisplayName = "Sendkeys";
        }

        [DisplayName("Base On")]
        [Category("Input.A")]
        [DefaultValue(BridgeSendKeysBaseOn.Tab)]
        public BridgeSendKeysBaseOn BaseOn { get; set; } = BridgeSendKeysBaseOn.Tab;

        [DisplayName("Input Tab")]
        [Description("Optional when Selector contains <wnd>.")]
        [Category("Input.B")]
        public InArgument<BwTab> InputTab { get; set; }

        [DisplayName("Target Type")]
        [Category("Input.C")]
        [DefaultValue(BridgeElementTargetType.Selector)]
        [TypeConverter("F2B.Browser.Chromium.Bridge.BridgeElementTargetTypeConverter, F2B.Browser.Chromium.Bridge")]
        public BridgeElementTargetType TargetType { get; set; } = BridgeElementTargetType.Selector;

        [DisplayName("Selector")]
        [Category("Input.D")]
        public InArgument<string> Selector { get; set; }

        [DisplayName("Input Element")]
        [Category("Input.E")]
        public InArgument<BwElement> InputElement { get; set; }

        [DisplayName("Delay Before (ms)")]
        [Category("Input.Z")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        [DisplayName("Timeout (ms)")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        [DisplayName("Keys")]
        [RequiredArgument]
        [Category("Input.F")]
        public InArgument<string> Keys { get; set; }

        [DisplayName("Interval (ms)")]
        [Category("Input.Z")]
        public InArgument<int?> Delay { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var keys = Keys.Get(context);
            var delay = Delay == null ? null : Delay.Get(context);

            if (BaseOn == BridgeSendKeysBaseOn.Tab)
            {
                var tab = InputTab == null ? null : InputTab.Get(context);
                if (tab == null)
                    throw new InvalidOperationException("InputTab must be provided when BaseOn=Tab.");
                tab.SendKeys(keys, delay);
                return;
            }

            var budget = new TimeoutBudget(BridgeActivityArgumentHelper.GetOrDefault(Timeout, context, 15000));
            var element = BridgeCompositeTargetResolver.ResolveElementTarget(
                context, TargetType, InputElement, Selector, InputTab, DelayBefore, budget.RemainingMs);
            if (budget.RemainingMs <= 0)
                throw new TimeoutException("Sendkeys timeout before operation.");
            element.SendKeys(keys, delay, budget.RemainingMs);
        }
    }
}
