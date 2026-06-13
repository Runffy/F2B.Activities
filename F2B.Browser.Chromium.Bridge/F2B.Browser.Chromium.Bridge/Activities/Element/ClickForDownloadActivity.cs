using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Click For Download")]
    [Description("Click an element and wait for download completion.")]
    [TypeDescriptionProvider(typeof(BridgeElementTargetTypeDescriptionProvider))]
    public sealed class ClickForDownloadActivity : BridgeElementTargetActivityBase
    {
        public ClickForDownloadActivity() : base("Click For Download") { }

        [DisplayName("Save As Path")]
        [RequiredArgument]
        [Category("Input.F")]
        public InArgument<string> SaveAsPath { get; set; }

        [DisplayName("Timeout (ms)")]
        [Category("Input.Z")]
        [DefaultValue(60000)]
        public InArgument<int> Timeout { get; set; } = 60000;

        [DisplayName("Download")]
        [Category("Output")]
        public OutArgument<BwDownloadInfo> Download { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var budget = new TimeoutBudget(BridgeActivityArgumentHelper.GetOrDefault(Timeout, context, 60000));
            var target = ResolveTargetElement(context, budget.RemainingMs);
            if (budget.RemainingMs <= 0)
                throw new TimeoutException("ClickForDownload timeout before operation.");
            var download = target.ClickForDownload(SaveAsPath.Get(context), budget.RemainingMs);
            Download?.Set(context, download);
        }
    }
}
