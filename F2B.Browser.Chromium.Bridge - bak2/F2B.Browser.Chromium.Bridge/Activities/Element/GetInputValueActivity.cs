using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Get Input Value")]
    [Description("Get the input value of the target element.")]
    [TypeDescriptionProvider(typeof(BridgeElementTargetTypeDescriptionProvider))]
    public sealed class GetInputValueActivity : BridgeElementTargetActivityBase
    {
        public GetInputValueActivity() : base("Get Input Value") { }

        [DisplayName("Value")]
        [Category("Output")]
        public OutArgument<string> Value { get; set; }

        [DisplayName("Timeout (ms)")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            Value?.Set(context, ResolveTargetElementWithTimeout(context, Timeout).GetInputValue(
                BridgeActivityArgumentHelper.GetOrDefault(Timeout, context, 15000)));
        }
    }
}
