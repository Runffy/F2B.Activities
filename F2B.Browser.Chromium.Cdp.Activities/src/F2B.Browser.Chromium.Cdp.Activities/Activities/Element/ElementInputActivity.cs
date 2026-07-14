using System.Activities;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Element-Input")]
    [Description("Type text into the target element.")]
    public sealed class ElementInputActivity : CdpElementTargetActivityBase
    {
        public ElementInputActivity()
            : base("Element-Input")
        {
        }

        [DisplayName("Value")]
        [Description("Text value to input.")]
        [RequiredArgument]
        [Category("Input.D")]
        public InArgument<object> Value { get; set; }

        [DisplayName("Clear Before Input")]
        [Description("When true, clears the element before typing.")]
        [Category("Input.D")]
        [DefaultValue(false)]
        public InArgument<bool> ClearBeforeInput { get; set; } = false;

        [DisplayName("Method")]
        [Description("Interaction method used for input.")]
        [Category("Input.D")]
        [DefaultValue(CdpInteractionMethod.Simulate)]
        [TypeConverter(typeof(CdpInteractionMethodConverter))]
        public CdpInteractionMethod Method { get; set; } = CdpInteractionMethod.Simulate;

        [DisplayName("Timeout (ms)")]
        [Description("Timeout in milliseconds for locating the target element.")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            var element = ResolveTargetElementWithTimeout(context, Timeout);
            var value = Value.Get(context);
            var clear = CdpActivityArgumentHelper.GetOrDefault(ClearBeforeInput, context, false);
            element.Input(value, clear, Method);
        }
    }
}
