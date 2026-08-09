using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Get Selected Row Index")]
    [Description("Get the selected row index of a DataGrid (-1 if none).")]
    public sealed class GetSelectedRowIndexActivity : CodeActivity
    {
        public GetSelectedRowIndexActivity()
        {
            DisplayName = "Get Selected Row Index";
        }

        [RequiredArgument]
        [DisplayName("Control Id")]
        [Category("Input.A")]
        public InArgument<string> ControlId { get; set; }

        [DisplayName("Row Index")]
        [Category("Output")]
        public OutArgument<int> RowIndex { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            RowIndex.Set(context, session.GetDataGridSelectedRowIndex(ControlId.Get(context)));
        }
    }
}
