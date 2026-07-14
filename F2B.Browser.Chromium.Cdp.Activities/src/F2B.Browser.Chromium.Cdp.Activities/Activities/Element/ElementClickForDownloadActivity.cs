using System.Activities;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Element-Click-ForDownload")]
    [Description("Click the target element and wait for download completion.")]
    public sealed class ElementClickForDownloadActivity : CdpElementTargetActivityBase
    {
        public ElementClickForDownloadActivity()
            : base("Element-Click-ForDownload")
        {
        }

        [DisplayName("Download Directory")]
        [Description("Directory to save the downloaded file.")]
        [Category("Input.D")]
        public InArgument<string> DownloadDirectory { get; set; }

        [DisplayName("Rename")]
        [Description("Optional file name override.")]
        [Category("Input.D")]
        public InArgument<string> Rename { get; set; }

        [DisplayName("Suffix")]
        [Description("Optional suffix appended to the downloaded file name.")]
        [Category("Input.D")]
        public InArgument<string> Suffix { get; set; }

        [DisplayName("New Tab")]
        [Description("When true, expects the download to open in a new tab.")]
        [Category("Input.D")]
        [DefaultValue(true)]
        public InArgument<bool> NewTab { get; set; } = true;

        [DisplayName("Local Target Exists")]
        [Description("Action when the target file already exists.")]
        [Category("Input.D")]
        [DefaultValue(CdpLocalFileExistsAction.Overwrite)]
        [TypeConverter(typeof(CdpLocalFileExistsActionConverter))]
        public CdpLocalFileExistsAction LocalTargetExists { get; set; } = CdpLocalFileExistsAction.Overwrite;

        [DisplayName("Button")]
        [Description("Mouse button used for the click.")]
        [Category("Input.E")]
        [DefaultValue(CdpMouseButton.Left)]
        [TypeConverter(typeof(CdpMouseButtonConverter))]
        public CdpMouseButton Button { get; set; } = CdpMouseButton.Left;

        [DisplayName("Method")]
        [Description("Interaction method used for the click.")]
        [Category("Input.E")]
        [DefaultValue(CdpInteractionMethod.Simulate)]
        [TypeConverter(typeof(CdpInteractionMethodConverter))]
        public CdpInteractionMethod Method { get; set; } = CdpInteractionMethod.Simulate;

        [DisplayName("Count")]
        [Description("Number of clicks.")]
        [Category("Input.E")]
        [DefaultValue(1)]
        public InArgument<int> Count { get; set; } = 1;

        [DisplayName("Timeout (ms)")]
        [Description("Timeout in milliseconds for locating the element and waiting for download.")]
        [Category("Input.Z")]
        [DefaultValue(60000)]
        public InArgument<int> Timeout { get; set; } = 60000;

        [DisplayName("Actual Download File Path")]
        [Description("Outputs the saved download file path.")]
        [Category("Output")]
        public OutArgument<string> ActualDownloadFilePath { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var timeoutMs = CdpActivityArgumentHelper.GetOrDefault(Timeout, context, 60000);
            var element = ResolveTargetElement(context, timeoutMs);
            var downloadDirectory = DownloadDirectory == null ? null : DownloadDirectory.Get(context);
            var rename = Rename == null ? null : Rename.Get(context);
            var suffix = Suffix == null ? null : Suffix.Get(context);
            var newTab = CdpActivityArgumentHelper.GetOrDefault(NewTab, context, true);
            var count = CdpActivityArgumentHelper.GetOrDefault(Count, context, 1);

            var result = element.ClickToDownload(
                downloadDirectory,
                rename,
                suffix,
                newTab,
                LocalTargetExists,
                Button,
                Method,
                count,
                timeoutMs);

            ActualDownloadFilePath?.Set(context, result == null ? null : result.Path);
        }
    }
}
