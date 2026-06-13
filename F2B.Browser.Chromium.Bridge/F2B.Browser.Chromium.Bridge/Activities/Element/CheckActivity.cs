using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Check")]
    [Description("Check the target checkbox or radio element.")]
    [TypeDescriptionProvider(typeof(BridgeElementTargetTypeDescriptionProvider))]
    public sealed class CheckActivity : BridgeElementTargetActivityBase
    {
        public CheckActivity() : base("Check") { }

        [DisplayName("Timeout (ms)")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            ResolveTargetElementWithTimeout(context, Timeout).Check(
                BridgeActivityArgumentHelper.GetOrDefault(Timeout, context, 15000));
        }
    }
}
