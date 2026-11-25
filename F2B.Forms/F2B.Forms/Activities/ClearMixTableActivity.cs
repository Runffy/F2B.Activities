using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [ToolboxPath("MixTable")]
    [DisplayName("Clear MixTable")]
    [Description("Remove all MixTable rows. Columns and schema are kept.")]
    public sealed class ClearMixTableActivity : CodeActivity
    {
        public ClearMixTableActivity()
        {
            DisplayName = "Clear MixTable";
        }

        [RequiredArgument]
        [DisplayName("Control Id")]
        [Category("Input.A")]
        public InArgument<string> ControlId { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            session.ClearMixTable(ControlId.Get(context));
        }
    }
}
