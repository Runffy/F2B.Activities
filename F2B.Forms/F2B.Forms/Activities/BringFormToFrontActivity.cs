using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Bring Form To Front")]
    [Description("Soft-activate the form (temporary TopMost flash, then clear). Brings the form to the front once; other windows can cover it again when activated. Not sticky TopMost.")]
    public sealed class BringFormToFrontActivity : CodeActivity
    {
        public BringFormToFrontActivity()
        {
            DisplayName = "Bring Form To Front";
        }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            session.BringFormToFrontSoft();
        }
    }
}
