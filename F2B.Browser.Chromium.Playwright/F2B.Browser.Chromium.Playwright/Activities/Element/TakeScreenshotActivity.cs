using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Take Screenshot")]
    [Description("Capture a screenshot of the target element area.")]
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    public sealed class TakeScreenshotActivity : ElementTargetActivityBase
    {
        [DisplayName("Path")]
        [Description("File path where the screenshot is saved.")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<string> Path { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ResolveTargetElement(context).TakeScreenshot(Path.Get(context));
        }
    }
}
