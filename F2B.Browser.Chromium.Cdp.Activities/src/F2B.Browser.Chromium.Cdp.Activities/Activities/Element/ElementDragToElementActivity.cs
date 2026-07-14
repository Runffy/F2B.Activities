using System;
using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Element-DragToElement")]
    [Description("Drag a source element to a destination element.")]
    [Designer(typeof(CdpElementTargetActivityDesigner))]
    public sealed class ElementDragToElementActivity : CodeActivity
    {
        public ElementDragToElementActivity()
        {
            DisplayName = "Element-DragToElement";
        }

        [DisplayName("Parent Object")]
        [Description("Optional parent/root for the drag source.")]
        [Category("Input.A")]
        public InArgument<CdpBase> ParentObject { get; set; }

        [DisplayName("Selector")]
        [Description("Selector for the drag source element.")]
        [Category("Input.B")]
        public InArgument<string> Selector { get; set; }

        [DisplayName("Destination Parent Object")]
        [Description("Optional parent/root for the drop target.")]
        [Category("Input.C")]
        public InArgument<CdpBase> DestinationParentObject { get; set; }

        [DisplayName("Destination Selector")]
        [Description("Selector for the drop target element.")]
        [Category("Input.D")]
        public InArgument<string> DestinationSelector { get; set; }

        [DisplayName("Duration")]
        [Category("Input.E")]
        [DefaultValue(0.5)]
        public InArgument<double> Duration { get; set; } = 0.5;

        [DisplayName("Timeout (ms)")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        [DisplayName("Delay Before (ms)")]
        [Category("Input.Z")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        protected override void Execute(CodeActivityContext context)
        {
            var timeoutMs = CdpActivityArgumentHelper.GetOrDefault(Timeout, context, 15000);
            var delayBefore = CdpActivityArgumentHelper.GetOrDefault(DelayBefore, context, 300);
            var sourceRoot = CdpTargetResolver.GetRoot(ParentObject, context, "ParentObject");
            var sourceSelector = Selector == null ? null : Selector.Get(context);
            var source = CdpTargetResolver.ResolveOperationElement(
                sourceRoot, sourceSelector, timeoutMs, delayBefore, false, false);

            var destRoot = CdpTargetResolver.GetRoot(DestinationParentObject, context, "DestinationParentObject");
            var destSelector = DestinationSelector == null ? null : DestinationSelector.Get(context);
            var destination = CdpTargetResolver.ResolveOperationElement(
                destRoot, destSelector, timeoutMs, 0, false, false);

            var duration = CdpActivityArgumentHelper.GetOrDefault(Duration, context, 0.5);
            source.DragToElement(destination, duration);
        }
    }
}
