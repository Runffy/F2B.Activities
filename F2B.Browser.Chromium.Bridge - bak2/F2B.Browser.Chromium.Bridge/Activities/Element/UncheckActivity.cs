using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Uncheck")]
    [Description("Uncheck the target checkbox element.")]
    [TypeDescriptionProvider(typeof(BridgeElementTargetTypeDescriptionProvider))]
    public sealed class UncheckActivity : BridgeElementTargetActivityBase
    {
        public UncheckActivity() : base("Uncheck") { }

        [DisplayName("Timeout (ms)")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            ResolveTargetElementWithTimeout(context, Timeout).Uncheck(
                BridgeActivityArgumentHelper.GetOrDefault(Timeout, context, 15000));
        }
    }
}
