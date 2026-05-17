using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Get Rect")]
    [Description("Get position and size information of the target element.")]
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    public sealed class GetRectActivity : ElementTargetActivityBase
    {
        [DisplayName("Rect")]
        [Description("Outputs the element bounding rectangle.")]
        [Category("Output")]
        public OutArgument<ElementRect> Rect { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Rect?.Set(context, ResolveTargetElement(context).GetRect());
        }
    }
}
