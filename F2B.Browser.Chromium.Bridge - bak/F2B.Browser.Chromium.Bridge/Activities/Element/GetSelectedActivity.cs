using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Get Selected")]
    [Description("Get selected option values from the target select element.")]
    [TypeDescriptionProvider(typeof(BridgeElementTargetTypeDescriptionProvider))]
    public sealed class GetSelectedActivity : BridgeElementTargetActivityBase
    {
        public GetSelectedActivity() : base("Get Selected") { }

        [DisplayName("Selected")]
        [Category("Output")]
        public OutArgument<string[]> Selected { get; set; }

        [DisplayName("Timeout (ms)")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            Selected?.Set(context, ResolveTargetElementWithTimeout(context, Timeout).GetSelected(
                BridgeActivityArgumentHelper.GetOrDefault(Timeout, context, 15000)));
        }
    }
}
