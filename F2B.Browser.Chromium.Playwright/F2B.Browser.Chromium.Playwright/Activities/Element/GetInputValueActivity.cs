using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Get Input Value")]
    [Description("Get the current value of an input element.")]
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    public sealed class GetInputValueActivity : ElementTargetActivityBase
    {
        public GetInputValueActivity() : base("Get Input Value") {}

        [DisplayName("Value")]
        [Description("Outputs the current input value.")]
        [Category("Output")]
        public OutArgument<string> Value { get; set; }

        [DisplayName("Timeout (ms)")]
        [Description("Timeout in milliseconds for locating the target element.")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            Value?.Set(context, ResolveTargetElementWithTimeout(context, Timeout).InputGetValue());
        }
    }
}
