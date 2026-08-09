using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Clear Table Cell")]
    [Description("Remove the control in a TableLayout cell, if any.")]
    public sealed class ClearTableCellActivity : CodeActivity
    {
        public ClearTableCellActivity()
        {
            DisplayName = "Clear Table Cell";
        }

        [RequiredArgument]
        [DisplayName("Table Id")]
        [Category("Input.A")]
        public InArgument<string> TableId { get; set; }

        [RequiredArgument]
        [DisplayName("Row")]
        [Category("Input.A")]
        public InArgument<int> Row { get; set; }

        [RequiredArgument]
        [DisplayName("Column")]
        [Category("Input.A")]
        public InArgument<int> Column { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            session.ClearTableCell(TableId.Get(context), Row.Get(context), Column.Get(context));
        }
    }
}
