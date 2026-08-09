using System.Activities;
using System.ComponentModel;
using System.Data;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Fill Table From DataTable")]
    [Description("Resize a TableLayout and fill cells from a DataTable. Default cell type is Label; list editable column names to use TextBox.")]
    public sealed class FillTableFromDataTableActivity : CodeActivity
    {
        public FillTableFromDataTableActivity()
        {
            DisplayName = "Fill Table From DataTable";
            HeaderRow = new InArgument<bool>(true);
        }

        [RequiredArgument]
        [DisplayName("Table Id")]
        [Category("Input.A")]
        public InArgument<string> TableId { get; set; }

        [RequiredArgument]
        [DisplayName("DataTable")]
        [Category("Input.A")]
        public InArgument<DataTable> DataTable { get; set; }

        [DisplayName("Header Row")]
        [Description("When true, row 0 shows column names as Labels.")]
        [Category("Input.B")]
        public InArgument<bool> HeaderRow { get; set; }

        [DisplayName("Editable Columns")]
        [Description("Comma-separated column names that become TextBox. Empty = all Labels.")]
        [Category("Input.B")]
        public InArgument<string> EditableColumns { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            session.FillTableFromDataTable(
                TableId.Get(context),
                DataTable.Get(context),
                HeaderRow.Get(context),
                EditableColumns.Get(context));
        }
    }
}
