using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Get Text")]
    [Description("Get the text content of the target element.")]
    [TypeDescriptionProvider(typeof(BridgeElementTargetTypeDescriptionProvider))]
    public sealed class GetTextActivity : BridgeElementTargetActivityBase
    {
        public GetTextActivity() : base("Get Text")
        {
        }

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
            var target = ResolveTargetElementWithTimeout(context, Timeout);
            Text?.Set(context, target.GetText());
        }
    }
}
