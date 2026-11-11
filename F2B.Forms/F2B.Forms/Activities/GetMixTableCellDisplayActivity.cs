using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [ToolboxPath("MixTable")]
    [DisplayName("Get MixTable Cell Display")]
    [Description("Returns Control | Text | Hidden for a MixTable cell.")]
    public sealed class GetMixTableCellDisplayActivity : CodeActivity<string>
    {
        public GetMixTableCellDisplayActivity()
        {
            DisplayName = "Get MixTable Cell Display";
        }

        [RequiredArgument]
        [DisplayName("Control Id")]
        [Category("Input.A")]
        public InArgument<string> ControlId { get; set; }

        [RequiredArgument]
        [DisplayName("Row Index")]
        [Category("Input.A")]
        public InArgument<int> RowIndex { get; set; }

        [RequiredArgument]
        [DisplayName("Column")]
        [Category("Input.A")]
        public InArgument<string> Column { get; set; }

        [DisplayName("Mode")]
        [Category("Output")]
        public OutArgument<string> Mode { get; set; }

        protected override string Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            string mode = session.GetMixTableCellDisplay(
                ControlId.Get(context),
                RowIndex.Get(context),
                Column.Get(context));
            Mode?.Set(context, mode);
            return mode;
        }
    }
}
