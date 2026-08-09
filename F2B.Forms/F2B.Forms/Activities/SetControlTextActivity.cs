using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Set Control Text")]
    [Description("Set the visible Text of Label, Button, CheckBox, TextBox, Form title, etc.")]
    public sealed class SetControlTextActivity : CodeActivity
    {
        public SetControlTextActivity()
        {
            DisplayName = "Set Control Text";
        }

        [RequiredArgument]
        [DisplayName("Control Id")]
        [Category("Input.A")]
        public InArgument<string> ControlId { get; set; }

        [RequiredArgument]
        [DisplayName("Text")]
        [Category("Input.A")]
        public InArgument<string> Text { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            session.SetControlText(ControlId.Get(context), Text.Get(context));
        }
    }
}
