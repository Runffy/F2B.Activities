using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [ToolboxPath("MixTable")]
    [DisplayName("Add MixTable Row")]
    [Description("Append one row. Values: Dictionary, DataRow, or JSON object {\"Col\":\"v\"}.")]
    public sealed class AddMixTableRowActivity : CodeActivity
    {
        public AddMixTableRowActivity()
        {
            DisplayName = "Add MixTable Row";
        }

        [RequiredArgument]
        [DisplayName("Control Id")]
        [Category("Input.A")]
        public InArgument<string> ControlId { get; set; }

        [DisplayName("Values")]
        [Description("Dictionary / DataRow / JSON object for cell values. Empty = defaults.")]
        [Category("Input.A")]
        public InArgument<object> Values { get; set; }

        [DisplayName("Row Index")]
        [Category("Output")]
        public OutArgument<int> RowIndex { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            int index = session.AddMixTableRow(ControlId.Get(context), Values.Get(context));
            RowIndex?.Set(context, index);
        }
    }
}
