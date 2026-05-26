using System;
using System.Activities;
using System.ComponentModel;
using Microsoft.Playwright;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Click")]
    [Description("Click the target element.")]
    [TypeDescriptionProvider(typeof(ClickValidationTypeDescriptionProvider))]
    public sealed class ClickActivity : ElementTargetActivityBase, IClickValidationConfig
    {
        public ClickActivity() : base("Click") {}

        [DisplayName("Button")]
        [Description("Mouse button used for clicking.")]
        [Category("Input.D")]
        [DefaultValue(MouseButton.Left)]
        [TypeConverter("F2B.Browser.Chromium.Playwright.MouseButtonTypeConverter, F2B.Browser.Chromium.Playwright")]
        public MouseButton Button { get; set; } = MouseButton.Left;

        [DisplayName("Count")]
        [Description("Number of consecutive clicks.")]
        [Category("Input.D")]
        [DefaultValue(1)]
        public InArgument<int> Count { get; set; } = 1;

        [DisplayName("Modifiers")]
        [Description("Keyboard modifiers used during click.")]
        [Category("Input.D")]
        public InArgument<string[]> Modifiers { get; set; }

        [DisplayName("Force")]
        [Description("Whether to force the click.")]
        [Category("Input.D")]
        [DefaultValue(false)]
        [TypeConverter("F2B.Browser.Chromium.Playwright.BooleanTypeConverter, F2B.Browser.Chromium.Playwright")]
        public bool Force { get; set; } = false;

        [DisplayName("Interval")]
        [Description("Delay in milliseconds between consecutive clicks when Count is greater than 1.")]
        [Category("Input.D")]
        [DefaultValue(0)]
        public InArgument<int> Interval { get; set; } = 0;

        [DisplayName("Validate")]
        [Description("Validation mode after clicking.")]
        [Category("Input.E")]
        [DefaultValue(ClickValidateMode.None)]
        [TypeConverter("F2B.Browser.Chromium.Playwright.ClickValidateModeTypeConverter, F2B.Browser.Chromium.Playwright")]
        public ClickValidateMode Validate
        {
            get => _validate;
            set
            {
                _validate = value;
                TypeDescriptor.Refresh(this);
            }
        }

        [DisplayName("Validation Selector")]
        [Description("Selector used to validate click results.")]
        [Category("Input.E")]
        public InArgument<string> ValidationSelector { get; set; }

        [DisplayName("Wait Before Validate")]
        [Description("Wait time in milliseconds after clicking before each validation check.")]
        [Category("Input.E")]
        [DefaultValue(1000)]
        public InArgument<int> WaitBeforeValidate { get; set; } = 1000;

        [DisplayName("Timeout (ms)")]
        [Description("Timeout in milliseconds for click and validation.")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        private ClickValidateMode _validate = ClickValidateMode.None;

        protected override void Execute(CodeActivityContext context)
        {
            var totalTimeout = ActivityArgumentHelper.GetOrDefault(Timeout, context, 15000);
            var budget = new TimeoutBudget(totalTimeout);
            var target = ResolveTargetElement(context, budget.RemainingAsNullableDouble());
            var remaining = budget.RemainingMs;
            if (remaining <= 0)
            {
                throw new TimeoutException("Click timeout before operation: no remaining timeout budget after locating target.");
            }

            target.Click(
                button: Button,
                count: ActivityArgumentHelper.GetOrDefault(Count, context, 1),
                interval: ActivityArgumentHelper.GetOrDefault(Interval, context, 0),
                modifiers: Modifiers == null ? null : Modifiers.Get(context),
                force: Force,
                validate: Validate,
                validationSelector: ValidationSelector == null ? null : ValidationSelector.Get(context),
                waitBeforeValidate: ActivityArgumentHelper.GetOrDefault(WaitBeforeValidate, context, 1000),
                timeout: remaining);
        }
    }
}
