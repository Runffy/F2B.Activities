using System.Activities;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Element-GetValue")]
    [Description("Get the value property from the target element.")]
    public sealed class ElementGetValueActivity : CdpElementTargetActivityBase
    {
        public ElementGetValueActivity()
            : base("Element-GetValue")
        {
        }

        [DisplayName("Value")]
        [Description("Outputs the element value.")]
        [Category("Output")]
        public OutArgument<string> Value { get; set; }

        [DisplayName("Timeout (ms)")]
        [Description("Timeout in milliseconds for locating the target element.")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            var element = ResolveTargetElementWithTimeout(context, Timeout);
            Value?.Set(context, element.Value);
        }
    }
}
