using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Create Control In Cell")]
    [Description("Create a control inside a TableLayout cell (row/column are zero-based). Clear the cell first if occupied.")]
    public sealed class CreateControlInCellActivity : CodeActivity
    {
        public CreateControlInCellActivity()
        {
            DisplayName = "Create Control In Cell";
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

        [RequiredArgument]
        [DisplayName("Type")]
        [Category("Input.B")]
        public InArgument<string> Type { get; set; }

        [RequiredArgument]
        [DisplayName("Control Id")]
        [Category("Input.B")]
        public InArgument<string> ControlId { get; set; }

        [DisplayName("Text")]
        [Category("Input.B")]
        public InArgument<string> Text { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            session.CreateControlInCell(
                TableId.Get(context),
                Row.Get(context),
                Column.Get(context),
                Type.Get(context),
                ControlId.Get(context),
                Text.Get(context));
        }
    }
}
