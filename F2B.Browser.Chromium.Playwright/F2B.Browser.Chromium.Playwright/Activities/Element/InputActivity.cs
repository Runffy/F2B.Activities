using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Input")]
    [Description("Type text into the target element.")]
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    public sealed class InputActivity : ElementTargetActivityBase
    {
        [DisplayName("Value")]
        [Description("Text value to input.")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<string> Value { get; set; }

        [DisplayName("Input Method")]
        [Description("Input mode used for typing text.")]
        [Category("Input")]
        [DefaultValue(F2B.Browser.Chromium.Playwright.InputMethod.Fill)]
        [TypeConverter("F2B.Browser.Chromium.Playwright.InputMethodTypeConverter, F2B.Browser.Chromium.Playwright")]
        public InputMethod InputMethod { get; set; } = F2B.Browser.Chromium.Playwright.InputMethod.Fill;

        [DisplayName("Type Delay")]
        [Description("Delay in milliseconds between characters.")]
        [Category("Input")]
        public InArgument<float?> TypeDelay { get; set; }

        [DisplayName("Validate Content After Inputted")]
        [Description("Whether to validate content after input.")]
        [Category("Input")]
        [DefaultValue(false)]
        [TypeConverter("F2B.Browser.Chromium.Playwright.BooleanTypeConverter, F2B.Browser.Chromium.Playwright")]
        public bool ValidateContentAfterInputted { get; set; } = false;

        [DisplayName("Interval")]
        [Description("Retry interval in milliseconds for validation.")]
        [Category("Input")]
        [DefaultValue(500)]
        public InArgument<int> Interval { get; set; } = 500;

        [DisplayName("Timeout (ms)")]
        [Description("Timeout in milliseconds for locating the target element and input validation.")]
        [Category("Input")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            var totalTimeout = ActivityArgumentHelper.GetOrDefault(Timeout, context, 15000);
            var budget = new TimeoutBudget(totalTimeout);
            var target = ResolveTargetElement(context, budget.RemainingAsNullableDouble());
            var remaining = budget.RemainingMs;
            if (remaining <= 0)
            {
                throw new TimeoutException("Input timeout before operation: no remaining timeout budget after locating target.");
            }

            target.Input(
                value: Value.Get(context),
                inputMethod: InputMethod,
                typeDelay: TypeDelay == null ? null : TypeDelay.Get(context),
                validateContentAfterInputted: ValidateContentAfterInputted,
                interval: ActivityArgumentHelper.GetOrDefault(Interval, context, 500),
                timeout: remaining);
        }
    }
}
