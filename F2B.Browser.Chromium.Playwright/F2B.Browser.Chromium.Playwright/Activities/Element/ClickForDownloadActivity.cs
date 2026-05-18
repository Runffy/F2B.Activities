using System;
using System.Activities;
using System.ComponentModel;
using Microsoft.Playwright;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Click For Download")]
    [Description("Click an element and wait for download completion.")]
    [TypeDescriptionProvider(typeof(ClickValidationTypeDescriptionProvider))]
    public sealed class ClickForDownloadActivity : ElementTargetActivityBase, IClickValidationConfig
    {
        [DisplayName("Save As Path")]
        [Description("Path where the downloaded file is saved.")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<string> SaveAsPath { get; set; }

        [DisplayName("Button")]
        [Description("Mouse button used for clicking.")]
        [Category("Input")]
        [DefaultValue(MouseButton.Left)]
        [TypeConverter("F2B.Browser.Chromium.Playwright.MouseButtonTypeConverter, F2B.Browser.Chromium.Playwright")]
        public MouseButton Button { get; set; } = MouseButton.Left;

        [DisplayName("Count")]
        [Description("Number of consecutive clicks.")]
        [Category("Input")]
        [DefaultValue(1)]
        public InArgument<int> Count { get; set; } = 1;

        [DisplayName("Interval")]
        [Description("Delay in milliseconds between clicks.")]
        [Category("Input")]
        [DefaultValue(500)]
        public InArgument<int> Interval { get; set; } = 500;

        [DisplayName("Validation Selector")]
        [Description("Selector used to validate click results.")]
        [Category("Input")]
        public InArgument<string> ValidationSelector { get; set; }

        [DisplayName("Modifiers")]
        [Description("Keyboard modifiers used during click.")]
        [Category("Input")]
        public InArgument<string[]> Modifiers { get; set; }

        [DisplayName("Force")]
        [Description("Whether to force the click.")]
        [Category("Input")]
        [DefaultValue(false)]
        [TypeConverter("F2B.Browser.Chromium.Playwright.BooleanTypeConverter, F2B.Browser.Chromium.Playwright")]
        public bool Force { get; set; } = false;

        [DisplayName("Validate")]
        [Description("Validation mode after clicking.")]
        [Category("Input")]
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

        [DisplayName("Timeout")]
        [Description("Timeout in milliseconds for waiting for download.")]
        [Category("Input")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        [DisplayName("Download")]
        [Description("Outputs download result information.")]
        [Category("Output")]
        public OutArgument<DownloadInfo> Download { get; set; }

        private ClickValidateMode _validate = ClickValidateMode.None;

        protected override void Execute(CodeActivityContext context)
        {
            var totalTimeout = ActivityArgumentHelper.GetOrDefault(Timeout, context, 15000);
            var budget = new TimeoutBudget(totalTimeout);
            var target = ResolveTargetElement(context, budget.RemainingAsNullableDouble());
            var remaining = budget.RemainingMs;
            if (remaining <= 0)
            {
                throw new TimeoutException("ClickForDownload timeout before operation: no remaining timeout budget after locating target.");
            }

            var download = target.ClickForDownload(
                saveAsPath: SaveAsPath == null ? null : SaveAsPath.Get(context),
                button: Button,
                count: ActivityArgumentHelper.GetOrDefault(Count, context, 1),
                interval: ActivityArgumentHelper.GetOrDefault(Interval, context, 500),
                modifiers: Modifiers == null ? null : Modifiers.Get(context),
                force: Force,
                validate: Validate,
                validationSelector: ValidationSelector == null ? null : ValidationSelector.Get(context),
                timeout: remaining);
            Download?.Set(context, download);
        }
    }
}
