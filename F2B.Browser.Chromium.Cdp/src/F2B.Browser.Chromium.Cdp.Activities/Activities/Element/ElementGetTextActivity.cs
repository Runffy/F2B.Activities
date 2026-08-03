using System.Activities;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Element-GetText")]
    [Description("Get text content from the target element.")]
    public sealed class ElementGetTextActivity : CdpElementTargetActivityBase
    {
        public ElementGetTextActivity()
            : base("Element-GetText")
        {
        }

        [DisplayName("Type")]
        [Description("Text content type.")]
        [Category("Input.D")]
        [DefaultValue(CdpElementTextType.InnerText)]
        [TypeConverter(typeof(CdpElementTextTypeConverter))]
        public CdpElementTextType Type { get; set; } = CdpElementTextType.InnerText;

        [DisplayName("Text")]
        [Description("Outputs the element text.")]
        [Category("Output")]
        public OutArgument<string> Text { get; set; }

        [DisplayName("Timeout (ms)")]
        [Description("Timeout in milliseconds for locating the target element.")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            var element = ResolveTargetElementWithTimeout(context, Timeout);
            string text;
            switch (Type)
            {
                case CdpElementTextType.OuterText:
                    text = element.Text;
                    break;
                case CdpElementTextType.RawOuterText:
                    text = element.RawText;
                    break;
                default:
                    text = element.InnerText;
                    break;
            }

            Text?.Set(context, text);
        }
    }
}
