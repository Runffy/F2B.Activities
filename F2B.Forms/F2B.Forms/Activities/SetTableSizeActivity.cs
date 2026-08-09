using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Set Table Size")]
    [Description("Set RowCount/ColumnCount of a TableLayout. Cells outside the new size are removed.")]
    public sealed class SetTableSizeActivity : CodeActivity
    {
        public SetTableSizeActivity()
        {
            DisplayName = "Set Table Size";
        }

        [RequiredArgument]
        [DisplayName("Table Id")]
        [Category("Input.A")]
        public InArgument<string> TableId { get; set; }

        [RequiredArgument]
        [DisplayName("Row Count")]
        [Category("Input.A")]
        public InArgument<int> RowCount { get; set; }

        [RequiredArgument]
        [DisplayName("Column Count")]
        [Category("Input.A")]
        public InArgument<int> ColumnCount { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            session.SetTableLayoutSize(
                TableId.Get(context),
                RowCount.Get(context),
                ColumnCount.Get(context));
        }
    }
}
