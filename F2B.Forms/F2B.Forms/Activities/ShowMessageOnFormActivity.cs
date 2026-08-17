using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Show Message On Form")]
    [Description("Show a native Win32 message box owned by the active form. Soft-brings the form to front first.")]
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
            FormSession session = FormSessionAccess.GetRequired(context);
            session.ShowMessage(Message.Get(context), Title.Get(context));
        }
    }
}
