using System.Activities;
using System.ComponentModel;
using System.Data;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [ToolboxPath("MixTable")]
    [DisplayName("Get MixTable DataTable")]
    [Description("Export the whole MixTable to a new DataTable (column names = MixTable column names).")]
    public sealed class GetMixTableDataTableActivity : CodeActivity<DataTable>
    {
        public GetMixTableDataTableActivity()
        {
            DisplayName = "Get MixTable DataTable";
        }

        [RequiredArgument]
        [DisplayName("Control Id")]
        [Category("Input.A")]
        public InArgument<string> ControlId { get; set; }

        [DisplayName("DataTable")]
        [Category("Output")]
        public OutArgument<DataTable> DataTable { get; set; }

        protected override DataTable Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            DataTable table = session.GetMixTableDataTable(ControlId.Get(context));
            DataTable?.Set(context, table);
            return table;
        }
    }
}
