using System.Activities;
using System.ComponentModel;

namespace Playwright.Activities
{
    public enum SelectValType
    {
        Text,
        Value,
        Index
    }

    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    [DisplayName("Element.Select")]
    public sealed class ElementSelectActivity : ElementTargetActivityBase
    {
        [Category("Input")]
        [DefaultValue(SelectValType.Text)]
        [TypeConverter("Playwright.Activities.SelectValTypeTypeConverter, Playwright.Activities")]
        public SelectValType ValType
        {
            get => _valType;
            set
            {
                _valType = value;
                TypeDescriptor.Refresh(this);
            }
        }

        [Category("Input")]
        public InArgument<string[]> Values { get; set; }

        [Category("Input")]
        public InArgument<string[]> Texts { get; set; }

        [Category("Input")]
        public InArgument<int[]> Indices { get; set; }

        [Category("Input")]
        [DefaultValue(false)]
        [TypeConverter("Playwright.Activities.BooleanTypeConverter, Playwright.Activities")]
        public bool ValidateContentAfterSelected { get; set; } = false;

        [Category("Input")]
        [DefaultValue(500)]
        public InArgument<int> Interval { get; set; } = 500;

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

            ResolveTargetElement(context).Select(
                values: values,
                texts: texts,
                indices: indices,
                validateContentAfterSelected: ValidateContentAfterSelected,
                interval: ActivityArgumentHelper.GetOrDefault(Interval, context, 500));
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);

            switch (ValType)
            {
                case SelectValType.Text:
                    if (Texts == null || Texts.Expression == null)
                    {
                        metadata.AddValidationError("ValType=Text 时必须填写 Texts。");
                    }

                    break;
                case SelectValType.Value:
                    if (Values == null || Values.Expression == null)
                    {
                        metadata.AddValidationError("ValType=Value 时必须填写 Values。");
                    }

                    break;
                case SelectValType.Index:
                    if (Indices == null || Indices.Expression == null)
                    {
                        metadata.AddValidationError("ValType=Index 时必须填写 Indices。");
                    }

                    break;
                default:
                    metadata.AddValidationError("不支持的 ValType。");
                    break;
            }
        }
    }
}
