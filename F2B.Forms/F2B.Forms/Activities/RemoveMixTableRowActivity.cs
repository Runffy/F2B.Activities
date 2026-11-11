using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [ToolboxPath("MixTable")]
    [DisplayName("Remove MixTable Row")]
    [Description("Remove a MixTable row by zero-based index. Typical source: F2B.Forms.GetLastRowIndex().")]
    public sealed class RemoveMixTableRowActivity : CodeActivity
    {
        public RemoveMixTableRowActivity()
        {
            DisplayName = "Remove MixTable Row";
        }

        [RequiredArgument]
        [DisplayName("Control Id")]
        [Category("Input.A")]
        public InArgument<string> ControlId { get; set; }

        [RequiredArgument]
        [DisplayName("Row Index")]
        [Category("Input.A")]
        public InArgument<int> RowIndex { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            session.RemoveMixTableRow(ControlId.Get(context), RowIndex.Get(context));
        }
    }
}
