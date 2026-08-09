using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Delete Control")]
    [Description("Delete a control by id (including nested children). The form itself cannot be deleted.")]
    public sealed class DeleteControlActivity : CodeActivity
    {
        public DeleteControlActivity()
        {
            DisplayName = "Delete Control";
        }

        [RequiredArgument]
        [DisplayName("Control Id")]
        [Category("Input.A")]
        public InArgument<string> ControlId { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            session.DeleteControl(ControlId.Get(context));
        }
    }
}
