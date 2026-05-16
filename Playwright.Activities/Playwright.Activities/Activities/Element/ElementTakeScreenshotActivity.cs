using System.Activities;
using System.ComponentModel;

namespace Playwright.Activities
{
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    [DisplayName("Element.TakeScreenshot")]
    public sealed class ElementTakeScreenshotActivity : ElementTargetActivityBase
    {
        [RequiredArgument]
        [Category("Input")]
        public InArgument<string> Path { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ResolveTargetElement(context).TakeScreenshot(Path.Get(context));
        }
    }
}
