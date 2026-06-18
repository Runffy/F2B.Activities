using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Is Checked")]
    [Description("Check whether the target element is checked.")]
    [TypeDescriptionProvider(typeof(BridgeElementTargetTypeDescriptionProvider))]
    public sealed class IsCheckedActivity : BridgeElementTargetActivityBase
    {
        public IsCheckedActivity() : base("Is Checked") { }

        [DisplayName("Checked")]
        [Category("Output")]
        public OutArgument<bool> Checked { get; set; }

        [DisplayName("Timeout (ms)")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            Checked?.Set(context, ResolveTargetElementWithTimeout(context, Timeout).IsChecked(
                BridgeActivityArgumentHelper.GetOrDefault(Timeout, context, 15000)));
        }
    }
}
