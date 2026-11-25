using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [ToolboxPath("Dialog")]
    [DisplayName("Show Message On Form")]
    [Description("Show a message box. Uses the active form as owner when inside AsyncForm; otherwise a standalone modal. Blocks until OK.")]
    public sealed class ShowMessageOnFormActivity : CodeActivity
    {
        public ShowMessageOnFormActivity()
        {
            DisplayName = "Show Message On Form";
        }

        [RequiredArgument]
        [DisplayName("Message")]
        [Category("Input.A")]
        public InArgument<string> Message { get; set; }

        [DisplayName("Title")]
        [Category("Input.A")]
        public InArgument<string> Title { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            FormDialogAccess.ShowMessage(
                context,
                Message.Get(context),
                Title == null ? null : Title.Get(context));
        }
    }
}
