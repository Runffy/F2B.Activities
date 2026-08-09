using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Set Control Value")]
    [Description("Set a control value/state (TextBox text, CheckBox checked, ComboBox selection).")]
    public sealed class SetControlValueActivity : CodeActivity
    {
        public SetControlValueActivity()
        {
            DisplayName = "Set Control Value";
        }

        [RequiredArgument]
        [DisplayName("Control Id")]
        [Category("Input.A")]
        public InArgument<string> ControlId { get; set; }

        [RequiredArgument]
        [DisplayName("Value")]
        [Category("Input.A")]
        public InArgument<object> Value { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            session.SetControlValue(ControlId.Get(context), Value.Get(context));
        }
    }
}
