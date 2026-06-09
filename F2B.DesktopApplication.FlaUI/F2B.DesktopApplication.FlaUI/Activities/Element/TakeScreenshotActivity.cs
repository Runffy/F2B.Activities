using System.Activities;
using System.ComponentModel;

namespace F2B.DesktopApplication.FlaUI
{
    [DisplayName("Take Screenshot")]
    [Description("Capture a screenshot of the target element's bounding rectangle.")]
    public sealed class TakeScreenshotActivity : FlaUiElementTargetActivityBase
    {
        [DisplayName("Output Path")]
        [Category("Input.E")]
        [RequiredArgument]
        public InArgument<string> OutputPath { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var outputPath = OutputPath == null ? null : OutputPath.Get(context);
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new System.ArgumentException("Output Path is required.");

            ResolveTargetElement(context).TakeScreenshot(outputPath);
        }
    }
}
