using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore.COM
{
    [DisplayName("Frame Exists")]
    [Description("Check whether frame path exists.")]
    public sealed class HasFrameActivity : IeWindowActivityBase
    {
        [Category("Time")]
        [DisplayName("Delay Before")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        [Category("Target")]
        [DisplayName("Frame Path (Json String)")]
        [RequiredArgument]
        public InArgument<string> FramePath { get; set; }

        [Category("Output")]
        [DisplayName("Exists")]
        public OutArgument<bool> Exists { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ActivityArgumentHelper.ApplyDelayBefore(DelayBefore, context, 300);

            var framePathJson = FramePath == null ? null : FramePath.Get(context);
            var framePath = ActivityArgumentHelper.ParseJsonArray(framePathJson, "Frame Path", required: true);
            var result = ResolveWindow(context).has_frame(framePath);
            Exists.Set(context, result);
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);

            if (FramePath == null || FramePath.Expression == null)
            {
                metadata.AddValidationError("Frame Path is required.");
            }
        }
    }
}
