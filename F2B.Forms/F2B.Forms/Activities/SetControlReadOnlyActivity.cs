using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Set Control ReadOnly")]
    [Description("Set a control ReadOnly state (TextBox / TextArea / MaskedTextBox). Choose True or False in the property pane dropdown.")]
    public sealed class SetControlReadOnlyActivity : CodeActivity
    {
        public SetControlReadOnlyActivity()
        {
            DisplayName = "Set Control ReadOnly";
            ReadOnly = false;
        }

        [RequiredArgument]
        [DisplayName("Control Id")]
        [Category("Input.A")]
        public InArgument<string> ControlId { get; set; }

        /// <summary>
        /// Plain bool (not InArgument) so the property pane shows a True/False dropdown.
        /// </summary>
        [DisplayName("Read Only")]
        [Description("True = read-only; False = editable.")]
        [Category("Input.A")]
        [DefaultValue(false)]
        public bool ReadOnly { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            session.SetControlReadOnly(ControlId.Get(context), ReadOnly);
        }
    }
}
