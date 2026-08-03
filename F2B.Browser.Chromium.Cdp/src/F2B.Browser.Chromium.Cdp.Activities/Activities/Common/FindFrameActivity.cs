using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Find Frame")]
    [Description("Find a matching frame under ParentObject or via a full <wnd> selector.")]
    [Designer(typeof(CdpCanvasFieldsActivityDesigner))]
    public sealed class FindFrameActivity : CodeActivity
    {
        public FindFrameActivity()
        {
            DisplayName = "Find Frame";
        }

        [DisplayName("Parent Object")]
        [Description("Optional search root (CdpTab / CdpFrame / CdpElement). Required when Selector has no <wnd>.")]
        [Category("Input.A")]
        public InArgument<CdpBase> ParentObject { get; set; }

        [DisplayName("Selector")]
        [Description("Selector XML that resolves to a frame (typically includes <frm>).")]
        [Category("Input.B")]
        public InArgument<string> Selector { get; set; }

        [DisplayName("Timeout (ms)")]
        [Description("Timeout in milliseconds for frame search.")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        [DisplayName("Throw Exception")]
        [Description("When false, returns null instead of throwing if no frame is found.")]
        [Category("Input.Z")]
        [DefaultValue(true)]
        public InArgument<bool> ThrowException { get; set; } = true;

        [DisplayName("Frame")]
        [Description("Outputs the found frame.")]
        [Category("Output")]
        public OutArgument<CdpFrame> FrameResult { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var root = CdpTargetResolver.GetRoot(ParentObject, context, "ParentObject");
            var selector = Selector == null ? null : Selector.Get(context);
            var timeoutMs = CdpActivityArgumentHelper.GetOrDefault(Timeout, context, 15000);
            var throwException = CdpActivityArgumentHelper.GetOrDefault(ThrowException, context, true);
            var found = CdpTargetResolver.FindFrame(root, selector, timeoutMs, throwException);
            FrameResult?.Set(context, found);
        }
    }
}
