using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Get Text")]
    [Description("Get the text content of the target element.")]
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    public sealed class GetTextActivity : ElementTargetActivityBase
    {
        public GetTextActivity() : base("Get Text") {}

        [DisplayName("Text")]
        [Description("Outputs the element text content.")]
        [Category("Output")]
        public OutArgument<string> Text { get; set; }

        [DisplayName("Timeout (ms)")]
        [Description("Timeout in milliseconds for locating the target element.")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            Text?.Set(context, ResolveTargetElementWithTimeout(context, Timeout).GetText());
        }
    }
}
