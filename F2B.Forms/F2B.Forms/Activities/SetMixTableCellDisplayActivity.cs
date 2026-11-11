using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [ToolboxPath("MixTable")]
    [DisplayName("Set MixTable Cell Display")]
    [Description("Show a cell as Control (editor), Text (read-only), or Hidden (blank). Mode is case-sensitive: Control|Text|Hidden.")]
    public sealed class SetMixTableCellDisplayActivity : CodeActivity
    {
        public SetMixTableCellDisplayActivity()
        {
            DisplayName = "Set MixTable Cell Display";
            Mode = new InArgument<string>("Text");
        }

        [RequiredArgument]
        [DisplayName("Control Id")]
        [Description("MixTable id.")]
        [Category("Input.A")]
        public InArgument<string> ControlId { get; set; }

        [RequiredArgument]
        [DisplayName("Row Index")]
        [Category("Input.A")]
        public InArgument<int> RowIndex { get; set; }

        [RequiredArgument]
        [DisplayName("Column")]
        [Description("Column name or index.")]
        [Category("Input.A")]
        public InArgument<string> Column { get; set; }

        [RequiredArgument]
        [DisplayName("Mode")]
        [Description("Control | Text | Hidden (case-sensitive).")]
        [Category("Input.A")]
        public InArgument<string> Mode { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            session.SetMixTableCellDisplay(
                ControlId.Get(context),
                RowIndex.Get(context),
                Column.Get(context),
                Mode.Get(context));
        }
    }
}
