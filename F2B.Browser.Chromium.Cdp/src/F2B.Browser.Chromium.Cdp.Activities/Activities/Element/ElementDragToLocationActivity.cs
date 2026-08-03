using System;
using System.Activities;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Element-DragToLocation")]
    [Description("Drag the target element to an absolute location.")]
    public sealed class ElementDragToLocationActivity : CdpElementTargetActivityBase
    {
        public ElementDragToLocationActivity()
            : base("Element-DragToLocation")
        {
        }

        [DisplayName("X")]
        [Description("Target X coordinate.")]
        [Category("Input.D")]
        public InArgument<int> X { get; set; }

        [DisplayName("Y")]
        [Description("Target Y coordinate.")]
        [Category("Input.D")]
        public InArgument<int> Y { get; set; }

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
            var x = X.Get(context);
            var y = Y.Get(context);
            var duration = CdpActivityArgumentHelper.GetOrDefault(Duration, context, 0.5);
            element.DragToLocation(Tuple.Create(x, y), duration);
        }
    }
}
