using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Close Form")]
    [Description("Close the active AsyncForm and end its RunLoop.")]
    public sealed class CloseFormActivity : CodeActivity
    {
        public CloseFormActivity()
        {
            DisplayName = "Close Form";
        }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            session.Close(FormCloseReason.CloseForm);
        }
    }
}
