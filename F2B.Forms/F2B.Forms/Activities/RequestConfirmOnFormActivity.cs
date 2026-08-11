using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Request Confirm On Form")]
    [Description("Show a Yes/No confirmation owned by the active form. Returns true when Yes is chosen.")]
    public sealed class RequestConfirmOnFormActivity : CodeActivity<bool>
    {
        public RequestConfirmOnFormActivity()
        {
            DisplayName = "Request Confirm On Form";
            YesText = new InArgument<string>("Yes");
            NoText = new InArgument<string>("No");
        }

        [RequiredArgument]
        [DisplayName("Message")]
        [Category("Input.A")]
        public InArgument<string> Message { get; set; }

        [DisplayName("Title")]
        [Category("Input.A")]
        public InArgument<string> Title { get; set; }

        [DisplayName("Yes Text")]
        [Category("Input.B")]
        public InArgument<string> YesText { get; set; }

        [DisplayName("No Text")]
        [Category("Input.B")]
        public InArgument<string> NoText { get; set; }

        [DisplayName("Confirmed")]
        [Category("Output")]
        public OutArgument<bool> Confirmed { get; set; }

        protected override bool Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            bool result = session.RequestConfirm(
                Message.Get(context),
                Title == null ? null : Title.Get(context),
                YesText == null ? null : YesText.Get(context),
                NoText == null ? null : NoText.Get(context));
            Confirmed?.Set(context, result);
            return result;
        }
    }
}
