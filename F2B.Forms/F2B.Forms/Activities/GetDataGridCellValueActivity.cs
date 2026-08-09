using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Get Cell Value")]
    [Description("Read a cell from a DataGrid by row index and column name or index.")]
    public sealed class GetDataGridCellValueActivity : CodeActivity
    {
        public GetDataGridCellValueActivity()
        {
            DisplayName = "Get Cell Value";
        }

        [RequiredArgument]
        [DisplayName("Control Id")]
        [Category("Input.A")]
        public InArgument<string> ControlId { get; set; }

        [RequiredArgument]
        [DisplayName("Row Index")]
        [Category("Input.A")]
        public InArgument<int> RowIndex { get; set; }

        [RequiredArgument]
        [DisplayName("Column")]
        [Description("Column name or zero-based index.")]
        [Category("Input.A")]
        public InArgument<string> Column { get; set; }

        [DisplayName("Value")]
        [Category("Output")]
        public OutArgument<object> Value { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            object value = session.GetDataGridCellValue(
                ControlId.Get(context),
                RowIndex.Get(context),
                Column.Get(context));
            Value.Set(context, value);
        }
    }
}
