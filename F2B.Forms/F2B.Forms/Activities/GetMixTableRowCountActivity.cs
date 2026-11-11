using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [ToolboxPath("MixTable")]
    [DisplayName("Get MixTable Row Count")]
    public sealed class GetMixTableRowCountActivity : CodeActivity<int>
    {
        public GetMixTableRowCountActivity()
        {
            DisplayName = "Get MixTable Row Count";
        }

        [RequiredArgument]
        [DisplayName("Control Id")]
        [Category("Input.A")]
        public InArgument<string> ControlId { get; set; }

        [DisplayName("Count")]
        [Category("Output")]
        public OutArgument<int> Count { get; set; }

        protected override int Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            int count = session.GetMixTableRowCount(ControlId.Get(context));
            Count?.Set(context, count);
            return count;
        }
    }
}
