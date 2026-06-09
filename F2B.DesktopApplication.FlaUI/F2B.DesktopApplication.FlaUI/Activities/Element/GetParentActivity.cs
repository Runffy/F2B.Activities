using System.Activities;
using System.ComponentModel;

namespace F2B.DesktopApplication.FlaUI
{
    [DisplayName("Get Parent")]
    [Description("Get the parent element of the target element.")]
    public sealed class GetParentActivity : FlaUiElementTargetActivityBase
    {
        [Category("Output")]
        [DisplayName("Parent")]
        public OutArgument<UiElement> Parent { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Parent.Set(context, ResolveTargetElement(context).GetParent());
        }
    }
}
