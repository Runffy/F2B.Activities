using System;
using System.Activities;
using System.ComponentModel;
using System.Activities.Presentation;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Run JavaScript")]
    [Description("Run JavaScript in a tab or element context.")]
    [Designer(typeof(BridgeCanvasFieldsActivityDesigner))]
    public sealed class RunJsActivity : CodeActivity
    {
        public RunJsActivity()
        {
            DisplayName = "Run JavaScript";
        }

        [DisplayName("Base On")]
        [Category("Input.A")]
        [DefaultValue(BridgeRunJsBaseOn.Element)]
        public BridgeRunJsBaseOn BaseOn { get; set; } = BridgeRunJsBaseOn.Element;

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

        [DisplayName("Script")]
        [RequiredArgument]
        [Category("Input.F")]
        public InArgument<string> Script { get; set; }

        [DisplayName("Arg")]
        [Category("Input.G")]
        public InArgument<object> Arg { get; set; }

        [DisplayName("Result")]
        [Category("Output")]
        public OutArgument<object> Result { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var script = Script.Get(context);
            var arg = Arg == null ? null : Arg.Get(context);
            object result;

            if (BaseOn == BridgeRunJsBaseOn.Tab)
            {
                var tab = InputTab == null ? null : InputTab.Get(context);
                if (tab == null)
                    throw new InvalidOperationException("InputTab must be provided when BaseOn=Tab.");
                result = tab.RunJs(script, arg, BridgeActivityArgumentHelper.GetOrDefault(Timeout, context, 15000));
            }
            else
            {
                var budget = new TimeoutBudget(BridgeActivityArgumentHelper.GetOrDefault(Timeout, context, 15000));
                var element = BridgeCompositeTargetResolver.ResolveElementTarget(
                    context, TargetType, InputElement, Selector, InputTab, DelayBefore, budget.RemainingMs);
                if (budget.RemainingMs <= 0)
                    throw new TimeoutException("RunJs timeout before execution.");
                result = element.RunJs(script, arg, budget.RemainingMs);
            }

            Result?.Set(context, result);
        }
    }
}
