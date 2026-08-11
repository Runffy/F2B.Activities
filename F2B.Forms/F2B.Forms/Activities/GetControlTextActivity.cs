using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Get Control Text")]
    [Description("Get the display text of a control. Expression (VB): F2B.Forms.GetControlText(\"ControlId\")")]
    public sealed class GetControlTextActivity : CodeActivity<string>
    {
        public GetControlTextActivity()
        {
            DisplayName = "Get Control Text";
        }

        [RequiredArgument]
        [DisplayName("Control Id")]
        [Category("Input.A")]
        public InArgument<string> ControlId { get; set; }

        [DisplayName("Text")]
        [Category("Output")]
        public OutArgument<string> Text { get; set; }

        protected override string Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            string text = session.GetControlText(ControlId.Get(context));
            Text?.Set(context, text);
            return text;
        }
    }
}
