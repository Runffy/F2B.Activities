using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore.COM
{
    [DisplayName("Wait For Frame")]
    [Description("Wait until target frame path becomes available.")]
    [Designer(typeof(RequiredFieldsActivityDesigner))]
    public sealed class WaitForFrameActivity : CodeActivity
    {
        [Category("Input")]
        [DisplayName("Input Window")]
        [RequiredArgument]
        public InArgument<IEWindowController> InputWindow { get; set; }

        [Category("Target")]
        [DisplayName("Frame Path (Json String)")]
        [RequiredArgument]
        public InArgument<string> FramePath { get; set; }

        [Category("Time")]
        [DisplayName("Delay Before")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        [Category("Time")]
        [DisplayName("Timeout")]
        [DefaultValue(60000)]
        public InArgument<int> Timeout { get; set; } = 60000;

        [Category("Time")]
        [DisplayName("Interval")]
        [DefaultValue(200)]
        public InArgument<int> Interval { get; set; } = 200;

        [Category("Output")]
        [DisplayName("Window")]
        public OutArgument<IEWindowController> Window { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ActivityArgumentHelper.ApplyDelayBefore(DelayBefore, context, 300);

            var window = InputWindow == null ? null : InputWindow.Get(context);
            if (window == null)
                throw new ArgumentException("InputWindow is required.");

            var framePathJson = FramePath == null ? null : FramePath.Get(context);
            var framePath = ActivityArgumentHelper.ParseJsonArray(framePathJson, "Frame Path", required: true);
            if (framePath == null)
                throw new ArgumentException("FramePath is required.");

            var result = window.wait_for_frame(
                frame_path: framePath,
                timeout: ActivityArgumentHelper.GetOrDefault(Timeout, context, 60000),
                interval: ActivityArgumentHelper.GetOrDefault(Interval, context, 200));

            Window.Set(context, result);
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);

            if (InputWindow == null || InputWindow.Expression == null)
            {
                metadata.AddValidationError("Input Window is required.");
            }

            if (FramePath == null || FramePath.Expression == null)
            {
                metadata.AddValidationError("Frame Path is required.");
            }
        }
    }
}
