using System;
using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Run JavaScript")]
    [Description("Run JavaScript on Target (Tab/Frame/Element) or an element resolved from Selector.")]
    [Designer(typeof(CdpCanvasFieldsActivityDesigner))]
    public sealed class RunJsActivity : CodeActivity
    {
        public RunJsActivity()
        {
            DisplayName = "Run JavaScript";
        }

        [DisplayName("Target")]
        [Description("Optional context root (CdpTab / CdpFrame / CdpElement).")]
        [Category("Input.A")]
        public InArgument<CdpBase> Target { get; set; }

        [DisplayName("Selector")]
        [Description("Optional selector to locate an element under Target, or a full <wnd> selector.")]
        [Category("Input.B")]
        public InArgument<string> Selector { get; set; }

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

        [DisplayName("Arguments")]
        [Category("Input.G")]
        public InArgument<object[]> Arguments { get; set; }

        [DisplayName("Is Async")]
        [Category("Input.G")]
        [DefaultValue(false)]
        public InArgument<bool> IsAsync { get; set; } = false;

        [DisplayName("Result")]
        [Category("Output")]
        public OutArgument<object> Result { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var script = Script.Get(context);
            var args = Arguments == null ? null : Arguments.Get(context);
            var isAsync = CdpActivityArgumentHelper.GetOrDefault(IsAsync, context, false);
            var budget = new TimeoutBudget(CdpActivityArgumentHelper.GetOrDefault(Timeout, context, 15000));
            var target = CdpTargetResolver.GetRoot(Target, context, "Target");
            var selector = Selector == null ? null : Selector.Get(context);
            var resolved = CdpTargetResolver.ResolveActionContext(
                target,
                selector,
                budget.RemainingMs,
                CdpActivityArgumentHelper.GetOrDefault(DelayBefore, context, 300),
                "Target");

            if (budget.RemainingMs <= 0)
            {
                throw new TimeoutException("RunJs timeout before execution.");
            }

            object result;
            if (resolved.Kind == CdpResolvedContextKind.Element)
            {
                result = resolved.Element.RunJs(script, args, false, isAsync, budget.RemainingMs);
            }
            else if (resolved.Kind == CdpResolvedContextKind.Frame)
            {
                result = resolved.Frame.RunJs(script, args, false, isAsync, budget.RemainingMs);
            }
            else
            {
                result = resolved.Tab.RunJs(script, args, false, isAsync, budget.RemainingMs);
            }

            Result?.Set(context, result);
        }
    }
}
