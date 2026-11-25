using System.Activities;
using System.ComponentModel;
using System.Data;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [ToolboxPath("MixTable")]
    [DisplayName("Get MixTable DataColumn")]
    [Description("Export one MixTable column as a DataColumn (owned by a new single-column DataTable; values in DataColumn.Table.Rows).")]
    public sealed class GetMixTableDataColumnActivity : CodeActivity<DataColumn>
    {
        public GetMixTableDataColumnActivity()
        {
            DisplayName = "Get MixTable DataColumn";
        }

        [RequiredArgument]
        [DisplayName("Control Id")]
        [Category("Input.A")]
        public InArgument<string> ControlId { get; set; }

        [RequiredArgument]
        [DisplayName("Column")]
        [Description("Column name or zero-based index.")]
        [Category("Input.A")]
        public InArgument<string> Column { get; set; }

        [DisplayName("DataColumn")]
        [Category("Output")]
        public OutArgument<DataColumn> DataColumn { get; set; }

        protected override DataColumn Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            DataColumn column = session.GetMixTableDataColumn(
                ControlId.Get(context),
                Column.Get(context));
            DataColumn?.Set(context, column);
            return column;
        }
    }
}
