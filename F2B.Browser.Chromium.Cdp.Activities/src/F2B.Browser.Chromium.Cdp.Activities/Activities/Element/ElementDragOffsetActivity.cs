using System.Activities;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Element-DragOffset")]
    [Description("Drag the target element by offset.")]
    public sealed class ElementDragOffsetActivity : CdpElementTargetActivityBase
    {
        public ElementDragOffsetActivity()
            : base("Element-DragOffset")
        {
        }

        [DisplayName("Offset X")]
        [Description("Horizontal drag offset in pixels.")]
        [Category("Input.D")]
        [DefaultValue(0)]
        public InArgument<int> OffsetX { get; set; } = 0;

        [DisplayName("Offset Y")]
        [Description("Vertical drag offset in pixels.")]
        [Category("Input.D")]
        [DefaultValue(0)]
        public InArgument<int> OffsetY { get; set; } = 0;

        [DisplayName("Duration")]
        [Description("Drag duration in seconds.")]
        [Category("Input.D")]
        [DefaultValue(0.5)]
        public InArgument<double> Duration { get; set; } = 0.5;

        [DisplayName("Timeout (ms)")]
        [Description("Timeout in milliseconds for locating the target element.")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            var element = ResolveTargetElementWithTimeout(context, Timeout);
            var offsetX = CdpActivityArgumentHelper.GetOrDefault(OffsetX, context, 0);
            var offsetY = CdpActivityArgumentHelper.GetOrDefault(OffsetY, context, 0);
            var duration = CdpActivityArgumentHelper.GetOrDefault(Duration, context, 0.5);
            element.Drag(offsetX, offsetY, duration);
        }
    }
}
