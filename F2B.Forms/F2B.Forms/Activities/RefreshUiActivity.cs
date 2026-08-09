using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Refresh UI")]
    [Description("Force the form to repaint immediately. Use after Set Control Text/Value when a long-running BindEvent handler would otherwise delay painting (e.g. Run → Running).")]
    public sealed class RefreshUiActivity : CodeActivity
    {
        public RefreshUiActivity()
        {
            DisplayName = "Refresh UI";
            PumpMessages = new InArgument<bool>(true);
        }

        [DisplayName("Pump Messages")]
        [Description("When true, also pumps the WinForms message queue (Application.DoEvents) so paints and pending UI messages are processed before the next activity.")]
        [Category("Input.A")]
        public InArgument<bool> PumpMessages { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            session.RefreshUi(PumpMessages.Get(context));
        }
    }
}
