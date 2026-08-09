using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Clear Table")]
    [Description("Remove all controls from a TableLayout (keeps row/column size).")]
    public sealed class ClearTableActivity : CodeActivity
    {
        public ClearTableActivity()
        {
            DisplayName = "Clear Table";
        }

        [RequiredArgument]
        [DisplayName("Table Id")]
        [Category("Input.A")]
        public InArgument<string> TableId { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            session.ClearTable(TableId.Get(context));
        }
    }
}
