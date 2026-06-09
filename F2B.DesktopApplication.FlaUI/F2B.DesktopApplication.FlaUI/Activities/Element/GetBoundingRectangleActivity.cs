using System.Activities;
using System.ComponentModel;
using System.Drawing;

namespace F2B.DesktopApplication.FlaUI
{
    [DisplayName("Get Bounding Rectangle")]
    [Description("Get the screen bounding rectangle of the target desktop element.")]
    public sealed class GetBoundingRectangleActivity : FlaUiElementTargetActivityBase
    {
        [Category("Output")]
        [DisplayName("Rectangle")]
        public OutArgument<Rectangle> Rectangle { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Rectangle.Set(context, ResolveTargetElement(context).GetBoundingRectangle());
        }
    }
}
