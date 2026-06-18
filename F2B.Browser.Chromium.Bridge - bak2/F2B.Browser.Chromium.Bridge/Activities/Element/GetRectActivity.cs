using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Get Rect")]
    [Description("Get the bounding rectangle of the target element.")]
    [TypeDescriptionProvider(typeof(BridgeElementTargetTypeDescriptionProvider))]
    public sealed class GetRectActivity : BridgeElementTargetActivityBase
    {
        public GetRectActivity() : base("Get Rect") { }

        [DisplayName("Rect")]
        [Category("Output")]
        public OutArgument<BwRect> Rect { get; set; }

        [DisplayName("Timeout (ms)")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            Rect?.Set(context, ResolveTargetElementWithTimeout(context, Timeout).GetRect(
                BridgeActivityArgumentHelper.GetOrDefault(Timeout, context, 15000)));
        }
    }
}
