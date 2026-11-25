using System.Activities;
using System.ComponentModel;
using System.Data;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [ToolboxPath("MixTable")]
    [DisplayName("Get MixTable DataRow")]
    [Description("Export one MixTable row as a DataRow (owned by a new single-row DataTable).")]
    public sealed class GetMixTableDataRowActivity : CodeActivity<DataRow>
    {
        public GetMixTableDataRowActivity()
        {
            DisplayName = "Get MixTable DataRow";
        }

        [RequiredArgument]
        [DisplayName("Control Id")]
        [Category("Input.A")]
        public InArgument<string> ControlId { get; set; }

        [RequiredArgument]
        [DisplayName("Row Index")]
        [Category("Input.A")]
        public InArgument<int> RowIndex { get; set; }

        [DisplayName("DataRow")]
        [Category("Output")]
        public OutArgument<DataRow> DataRow { get; set; }

        protected override DataRow Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            DataRow row = session.GetMixTableDataRow(
                ControlId.Get(context),
                RowIndex.Get(context));
            DataRow?.Set(context, row);
            return row;
        }
    }
}
