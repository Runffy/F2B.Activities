using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Input")]
    [Description("Type text into the target element.")]
    [TypeDescriptionProvider(typeof(BridgeElementTargetTypeDescriptionProvider))]
    public sealed class InputActivity : BridgeElementTargetActivityBase
    {
        public InputActivity() : base("Input")
        {
        }

        [DisplayName("Value")]
        [Description("Text value to input.")]
        [RequiredArgument]
        [Category("Input.D")]
        public InArgument<string> Value { get; set; }

        [DisplayName("Input Method")]
        [Description("Input mode used for typing text.")]
        [Category("Input.D")]
        [DefaultValue(BridgeInputMethod.Fill)]
        public BridgeInputMethod InputMethod { get; set; } = BridgeInputMethod.Fill;

        [DisplayName("Timeout (ms)")]
        [Description("Timeout in milliseconds for locating the target element and input.")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            var totalTimeout = BridgeActivityArgumentHelper.GetOrDefault(Timeout, context, 15000);
            var budget = new TimeoutBudget(totalTimeout);
            var target = ResolveTargetElement(context, budget.RemainingMs);
            if (budget.RemainingMs <= 0)
                throw new TimeoutException("Input timeout before operation.");

            target.Input(
                value: Value.Get(context),
                inputMethod: InputMethod,
                timeoutMs: budget.RemainingMs);
        }
    }
}
