using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;
using F2B.Forms;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [ToolboxPath("Control")]
    [DisplayName("Update")]
    [Description("Update any writable control property by name (e.g. SelectedIndex, Enabled, Text). Value/Items/ReadOnly/color aliases keep form semantics.")]
    public sealed class UpdateControlActivity : CodeActivity
    {
        public UpdateControlActivity()
        {
            DisplayName = "Update";
        }

        [RequiredArgument]
        [DisplayName("Control Id")]
        [Category("Input.A")]
        public InArgument<string> ControlId { get; set; }

        [RequiredArgument]
        [DisplayName("Property Name")]
        [Category("Input.A")]
        public InArgument<string> PropertyName { get; set; }

        [RequiredArgument]
        [DisplayName("Value")]
        [Category("Input.A")]
        public InArgument<object> Value { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            session.UpdateControlProperty(
                ControlId.Get(context),
                PropertyName.Get(context),
                Value.Get(context));
        }
    }
}
