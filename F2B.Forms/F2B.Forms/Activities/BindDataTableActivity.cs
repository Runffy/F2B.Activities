using System.Activities;
using System.ComponentModel;
using System.Data;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Bind DataTable")]
    [Description("Bind a DataTable to a read-only DataGrid. Do not mutate the table from another thread while the form is open.")]
    public sealed class BindDataTableActivity : CodeActivity
    {
        public BindDataTableActivity()
        {
            DisplayName = "Bind DataTable";
        }

        [RequiredArgument]
        [DisplayName("Control Id")]
        [Category("Input.A")]
        public InArgument<string> ControlId { get; set; }

        [RequiredArgument]
        [DisplayName("DataTable")]
        [Category("Input.A")]
        public InArgument<DataTable> DataTable { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            session.BindDataTable(ControlId.Get(context), DataTable.Get(context));
        }
    }
}
