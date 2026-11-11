using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [ToolboxPath("MixTable")]
    [DisplayName("Set MixTable Column")]
    [Description("Update one column. Scalar value fills all rows; a sequence updates by row index (extra items ignored; shorter sequence leaves remaining rows unchanged).")]
    public sealed class SetMixTableColumnActivity : CodeActivity
    {
        public SetMixTableColumnActivity()
        {
            DisplayName = "Set MixTable Column";
        }

        [RequiredArgument]
        [DisplayName("Control Id")]
        [Description("MixTable id.")]
        [Category("Input.A")]
        public InArgument<string> ControlId { get; set; }

        [RequiredArgument]
        [DisplayName("Column")]
        [Description("Column name or zero-based index.")]
        [Category("Input.A")]
        public InArgument<string> Column { get; set; }

        [RequiredArgument]
        [DisplayName("Values")]
        [Description("One value for the whole column, or a sequence (array/list) aligned to rows from index 0.")]
        [Category("Input.A")]
        public InArgument<object> Values { get; set; }

        [DisplayName("Updated Count")]
        [Description("Number of MixTable rows that were written.")]
        [Category("Output")]
        public OutArgument<int> UpdatedCount { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            int updated = session.SetMixTableColumnValues(
                ControlId.Get(context),
                Column.Get(context),
                Values.Get(context));
            UpdatedCount?.Set(context, updated);
        }
    }
}
