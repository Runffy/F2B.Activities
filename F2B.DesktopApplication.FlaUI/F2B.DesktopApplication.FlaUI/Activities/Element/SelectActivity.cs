using System.Activities;
using System.ComponentModel;

namespace F2B.DesktopApplication.FlaUI
{
    [DisplayName("Select")]
    [Description("Select an option from combo box or list box.")]
    public sealed class SelectActivity : FlaUiElementTargetActivityBase
    {
        [Category("Input.E")]
        [DisplayName("Text")]
        public InArgument<string> Text { get; set; }

        [Category("Input.E")]
        [DisplayName("Value")]
        public InArgument<string> Value { get; set; }

        [Category("Input.E")]
        [DisplayName("Index")]
        public InArgument<int?> Index { get; set; }

        [Category("Input.E")]
        [DisplayName("Text Contains")]
        public InArgument<string> TextContains { get; set; }

        [Category("Input.E")]
        [DisplayName("Text Regex")]
        public InArgument<string> TextRegex { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ResolveTargetElement(context).Select(
                Text == null ? null : Text.Get(context),
                Value == null ? null : Value.Get(context),
                Index == null ? null : Index.Get(context),
                TextContains == null ? null : TextContains.Get(context),
                TextRegex == null ? null : TextRegex.Get(context));
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);

            var hasText = ActivityArgumentHelper.HasTextExpression(Text);
            var hasValue = ActivityArgumentHelper.HasTextExpression(Value);
            var hasIndex = Index != null && Index.Expression != null;
            var hasTextContains = ActivityArgumentHelper.HasTextExpression(TextContains);
            var hasTextRegex = ActivityArgumentHelper.HasTextExpression(TextRegex);

            if (!hasText && !hasValue && !hasIndex && !hasTextContains && !hasTextRegex)
                metadata.AddValidationError("At least one select condition is required: Text, Value, Index, Text Contains, or Text Regex.");
        }
    }
}
