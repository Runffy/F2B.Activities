using System.Activities;
using System.ComponentModel;

namespace F2B.DesktopApplication.FlaUI
{
    [DisplayName("Double Click")]
    [Description("Double-click the target desktop element.")]
    public sealed class DoubleClickActivity : FlaUiElementTargetActivityBase
    {
        [DisplayName("Button")]
        [Category("Input.D")]
        [DefaultValue(MouseButton.Left)]
        [TypeConverter(typeof(MouseButtonTypeConverter))]
        public MouseButton Button { get; set; } = MouseButton.Left;

        protected override void Execute(CodeActivityContext context)
        {
            ResolveTargetElement(context).DoubleClick(Button);
        }
    }
}
