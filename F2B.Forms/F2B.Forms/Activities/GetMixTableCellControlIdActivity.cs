using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [ToolboxPath("MixTable")]
    [DisplayName("Get MixTable Cell Control Id")]
    [Description("Returns a virtual control id for a MixTable cell. Pass it to Get/Set Control Value or Get/Set Text.")]
    public sealed class GetMixTableCellControlIdActivity : CodeActivity<string>
    {
        public GetMixTableCellControlIdActivity()
        {
            DisplayName = "Get MixTable Cell Control Id";
        }

        [RequiredArgument]
        [DisplayName("Control Id")]
        [Description("MixTable control id.")]
        [Category("Input.A")]
        public InArgument<string> ControlId { get; set; }

        [RequiredArgument]
        [DisplayName("Row Index")]
        [Category("Input.A")]
        public InArgument<int> RowIndex { get; set; }

        [RequiredArgument]
        [DisplayName("Column")]
        [Description("Column name or zero-based index.")]
        [Category("Input.A")]
        public InArgument<string> Column { get; set; }

        [DisplayName("Cell Control Id")]
        [Category("Output")]
        public OutArgument<string> CellControlId { get; set; }

        protected override string Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            string id = session.GetMixTableCellControlId(
                ControlId.Get(context),
                RowIndex.Get(context),
                Column.Get(context));
            CellControlId?.Set(context, id);
            return id;
        }
    }
}
