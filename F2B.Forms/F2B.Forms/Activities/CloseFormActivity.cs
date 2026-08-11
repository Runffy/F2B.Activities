using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Close Form")]
    [Description("Close the active AsyncForm from a Bind Event (or other non-Close-Scope) handler when a condition is met. Idempotent if already closed.")]
    public sealed class CloseFormActivity : CodeActivity
    {
        public CloseFormActivity()
        {
            DisplayName = "Close Form";
        }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.TryGet(context);
            if (session == null || session.IsClosed)
            {
                // Already closed (e.g. Close Scope auto-close) — no-op.
                return;
            }

            session.Close(FormCloseReason.CloseForm);
        }
    }
}
