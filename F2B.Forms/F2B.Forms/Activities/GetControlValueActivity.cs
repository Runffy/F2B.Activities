using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;
using F2B.Forms;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [ToolboxPath("Control")]
    [DisplayName("Get Value")]
    public sealed class GetControlValueActivity : CodeActivity<object>
    {
        public GetControlValueActivity()
        {
            DisplayName = "Get Value";
        }

        [RequiredArgument]
        [DisplayName("Control Id")]
        [Category("Input.A")]
        public InArgument<string> ControlId { get; set; }


        [DisplayName("Value")]
        [Category("Output")]
        public OutArgument<object> Value { get; set; }

        protected override object Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            object value = session.GetControlValue(ControlId.Get(context));
            Value?.Set(context, value);
            return value;
        }
    }
}
