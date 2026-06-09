using System.Activities;
using System.ComponentModel;

namespace F2B.DesktopApplication.FlaUI
{
    [DisplayName("Get Property")]
    [Description("Read a UIA property from the target desktop element.")]
    public sealed class GetPropertyActivity : FlaUiElementTargetActivityBase
    {
        [DisplayName("Property Name")]
        [Category("Input.D")]
        [DefaultValue("Name")]
        [RequiredArgument]
        public InArgument<string> PropertyName { get; set; }

        [Category("Output")]
        [DisplayName("Value")]
        public OutArgument<string> Value { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var propertyName = PropertyName == null ? null : PropertyName.Get(context);
            Value.Set(context, ResolveTargetElement(context).GetProperty(propertyName));
        }
    }
}
