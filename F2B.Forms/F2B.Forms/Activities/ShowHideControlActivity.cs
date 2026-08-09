using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Show Hide Control")]
    [Description("Show or hide a control. Set Visible=false to hide.")]
    public sealed class ShowHideControlActivity : CodeActivity
    {
        public ShowHideControlActivity()
        {
            DisplayName = "Show Hide Control";
            Visible = new InArgument<bool>(true);
        }

        [RequiredArgument]
        [DisplayName("Control Id")]
        [Category("Input.A")]
        public InArgument<string> ControlId { get; set; }

        [RequiredArgument]
        [DisplayName("Visible")]
        [Category("Input.A")]
        public InArgument<bool> Visible { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            session.SetControlVisible(ControlId.Get(context), Visible.Get(context));
        }
    }
}
