using System.Activities;
using System.ComponentModel;

namespace F2B.DesktopApplication.FlaUI
{
    [DisplayName("Get Children")]
    [Description("Get direct child elements of the target element.")]
    public sealed class GetChildrenActivity : FlaUiElementTargetActivityBase
    {
        [Category("Output")]
        [DisplayName("Elements")]
        public OutArgument<UiElement[]> Elements { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Elements.Set(context, ResolveTargetElement(context).GetChildren());
        }
    }
}
