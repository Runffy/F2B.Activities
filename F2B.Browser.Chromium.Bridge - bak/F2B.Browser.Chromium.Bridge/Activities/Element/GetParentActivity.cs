using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Get Parent")]
    [Description("Get the parent element of the target element.")]
    [TypeDescriptionProvider(typeof(BridgeElementTargetTypeDescriptionProvider))]
    public sealed class GetParentActivity : BridgeElementTargetActivityBase
    {
        public GetParentActivity() : base("Get Parent") { }

        [DisplayName("Level")]
        [Category("Input.D")]
        [DefaultValue(1)]
        public InArgument<int> Level { get; set; } = 1;

        [DisplayName("Parent")]
        [Category("Output")]
        public OutArgument<BwElement> Parent { get; set; }

        [DisplayName("Timeout (ms)")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            Parent?.Set(context, ResolveTargetElementWithTimeout(context, Timeout).GetParent(
                BridgeActivityArgumentHelper.GetOrDefault(Level, context, 1),
                BridgeActivityArgumentHelper.GetOrDefault(Timeout, context, 15000)));
        }
    }
}
