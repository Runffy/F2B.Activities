using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore.COM
{
    [DisplayName("Select")]
    [Description("Select option from dropdown.")]
    public sealed class SelectOptionActivity : IeElementActivityBase
    {
        [Category("Select")]
        [DisplayName("Text")]
        public InArgument<string> Text { get; set; }

        [Category("Select")]
        [DisplayName("Value")]
        public InArgument<string> Value { get; set; }

        [Category("Select")]
        [DisplayName("Index")]
        public InArgument<int?> Index { get; set; }

        [Category("Select")]
        [DisplayName("Text Contains")]
        public InArgument<string> TextContains { get; set; }

        [Category("Select")]
        [DisplayName("Text Regex")]
        public InArgument<string> TextRegex { get; set; }

        [Category("Select")]
        [DisplayName("Trigger Events")]
        [DefaultValue(true)]
        public InArgument<bool> TriggerEvents { get; set; } = true;

        [Category("Select")]
        [DisplayName("Trigger Double Click")]
        [DefaultValue(false)]
        public InArgument<bool> TriggerDoubleClick { get; set; } = false;

        [Category("Time")]
        [DisplayName("Delay Before")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        [Category("Time")]
        [DisplayName("Delay After")]
        [DefaultValue(0)]
        public InArgument<int> DelayAfter { get; set; } = 0;

        protected override void Execute(CodeActivityContext context)
        {
            ResolveWindow(context).select_option(
                locator: ResolveSelector(context),
                frame_path: ResolveFramePath(context),
                timeout: ResolveTimeout(context),
                text: Text == null ? null : Text.Get(context),
                value: Value == null ? null : Value.Get(context),
                index: Index == null ? null : Index.Get(context),
                text_contains: TextContains == null ? null : TextContains.Get(context),
                text_re: TextRegex == null ? null : TextRegex.Get(context),
                trigger_events: ActivityArgumentHelper.GetOrDefault(TriggerEvents, context, true),
                trigger_dblclick: ActivityArgumentHelper.GetOrDefault(TriggerDoubleClick, context, false),
                delay_before: ActivityArgumentHelper.GetOrDefault(DelayBefore, context, 300),
                delay_after: ActivityArgumentHelper.GetOrDefault(DelayAfter, context, 0));
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);

            var hasText = HasTextExpression(Text);
            var hasValue = HasTextExpression(Value);
            var hasIndex = HasExpression(Index);
            var hasTextContains = HasTextExpression(TextContains);
            var hasTextRegex = HasTextExpression(TextRegex);

            if (!hasText && !hasValue && !hasIndex && !hasTextContains && !hasTextRegex)
            {
                metadata.AddValidationError("At least one select condition is required: Text, Value, Index, Text Contains, or Text Regex.");
            }
        }
    }
}
