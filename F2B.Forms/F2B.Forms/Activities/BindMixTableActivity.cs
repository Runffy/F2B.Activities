using System.Activities;
using System.ComponentModel;
using System.Data;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [ToolboxPath("MixTable")]
    [DisplayName("Bind MixTable")]
    [Description("Fill MixTable rows from a DataTable. Column names should match MixTable column names.")]
    public sealed class BindMixTableActivity : CodeActivity
    {
        public BindMixTableActivity()
        {
            DisplayName = "Bind MixTable";
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
            session.BindMixTable(ControlId.Get(context), DataTable.Get(context));
        }
    }
}
