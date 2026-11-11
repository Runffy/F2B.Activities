using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [ToolboxPath("MixTable")]
    [DisplayName("Set MixTable Cell")]
    public sealed class SetMixTableCellActivity : CodeActivity
    {
        public SetMixTableCellActivity()
        {
            DisplayName = "Set MixTable Cell";
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

        [RequiredArgument]
        [DisplayName("Value")]
        [Category("Input.A")]
        public InArgument<object> Value { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            session.SetMixTableCellValue(
                ControlId.Get(context),
                RowIndex.Get(context),
                Column.Get(context),
                Value.Get(context));
        }
    }
}
