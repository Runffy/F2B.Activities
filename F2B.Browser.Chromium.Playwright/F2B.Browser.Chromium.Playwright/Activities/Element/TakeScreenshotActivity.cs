using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Take Screenshot")]
    [Description("Capture a screenshot of the target element area.")]
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    public sealed class TakeScreenshotActivity : ElementTargetActivityBase
    {
        public TakeScreenshotActivity() : base("Take Screenshot") {}

        [DisplayName("Path")]
        [Description("File path where the screenshot is saved.")]
        [RequiredArgument]
        [Category("Input.D")]
        public InArgument<string> Path { get; set; }

        [DisplayName("Timeout (ms)")]
        [Description("Timeout in milliseconds for locating the target element.")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            ResolveTargetElementWithTimeout(context, Timeout).TakeScreenshot(Path.Get(context));
        }
    }
}
