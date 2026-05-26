using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    public enum SelectValType
    {
        Text,
        Value,
        Index
    }

    [DisplayName("Select")]
    [Description("Select specified options in the target select element.")]
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    public sealed class SelectActivity : ElementTargetActivityBase
    {
        public SelectActivity() : base("Select") {}

        [DisplayName("Value Type")]
        [Description("Select options by text, value, or index.")]
        [Category("Input.D")]
        [DefaultValue(SelectValType.Text)]
        [TypeConverter("F2B.Browser.Chromium.Playwright.SelectValTypeTypeConverter, F2B.Browser.Chromium.Playwright")]
        public SelectValType ValType
        {
            get => _valType;
            set
            {
                _valType = value;
                TypeDescriptor.Refresh(this);
            }
        }

        [DisplayName("Values")]
        [Description("Option values used when selecting by value.")]
        [Category("Input.E")]
        public InArgument<string[]> Values { get; set; }

        [DisplayName("Texts")]
        [Description("Option texts used when selecting by text.")]
        [Category("Input.E")]
        public InArgument<string[]> Texts { get; set; }

        [DisplayName("Indices")]
        [Description("Option indices used when selecting by index.")]
        [Category("Input.E")]
        public InArgument<int[]> Indices { get; set; }

        [DisplayName("Validate Content After Selected")]
        [Description("Whether to validate selected options after selection.")]
        [Category("Input.F")]
        [DefaultValue(false)]
        [TypeConverter("F2B.Browser.Chromium.Playwright.BooleanTypeConverter, F2B.Browser.Chromium.Playwright")]
        public bool ValidateContentAfterSelected { get; set; } = false;

        [DisplayName("Wait Before Validate")]
        [Description("Retry interval in milliseconds for validation.")]
        [Category("Input.F")]
        [DefaultValue(500)]
        public InArgument<int> Interval { get; set; } = 500;

        [DisplayName("Timeout (ms)")]
        [Description("Timeout in milliseconds for locating the target element and selection validation.")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        private SelectValType _valType = SelectValType.Text;

        protected override void Execute(CodeActivityContext context)
        {
            var values = default(string[]);
            var texts = default(string[]);
            var indices = default(int[]);

            switch (ValType)
            {
                case SelectValType.Text:
                    texts = Texts == null ? null : Texts.Get(context);
                    break;
                case SelectValType.Value:
                    values = Values == null ? null : Values.Get(context);
                    break;
                case SelectValType.Index:
                    indices = Indices == null ? null : Indices.Get(context);
                    break;
            }

            var totalTimeout = ActivityArgumentHelper.GetOrDefault(Timeout, context, 15000);
            var budget = new TimeoutBudget(totalTimeout);
            var target = ResolveTargetElement(context, budget.RemainingAsNullableDouble());
            var remaining = budget.RemainingMs;
            if (remaining <= 0)
            {
                throw new TimeoutException("Select timeout before operation: no remaining timeout budget after locating target.");
            }

            target.Select(
                values: values,
                texts: texts,
                indices: indices,
                validateContentAfterSelected: ValidateContentAfterSelected,
                interval: ActivityArgumentHelper.GetOrDefault(Interval, context, 500),
                timeout: remaining);
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);

            switch (ValType)
            {
                case SelectValType.Text:
                    if (Texts == null || Texts.Expression == null)
                    {
                        metadata.AddValidationError("Texts must be provided when ValType=Text.");
                    }

                    break;
                case SelectValType.Value:
                    if (Values == null || Values.Expression == null)
                    {
                        metadata.AddValidationError("Values must be provided when ValType=Value.");
                    }

                    break;
                case SelectValType.Index:
                    if (Indices == null || Indices.Expression == null)
                    {
                        metadata.AddValidationError("Indices must be provided when ValType=Index.");
                    }

                    break;
                default:
                    metadata.AddValidationError("Unsupported ValType.");
                    break;
            }
        }
    }
}
