using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Enable Control")]
    [Description("Enable or disable a control. Set Enabled=false to disable.")]
    public sealed class EnableControlActivity : CodeActivity
    {
        public EnableControlActivity()
        {
            DisplayName = "Enable Control";
            Enabled = new InArgument<bool>(true);
        }

        [RequiredArgument]
        [DisplayName("Control Id")]
        [Category("Input.A")]
        public InArgument<string> ControlId { get; set; }

        [RequiredArgument]
        [DisplayName("Enabled")]
        [Category("Input.A")]
        public InArgument<bool> Enabled { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            session.SetControlEnabled(ControlId.Get(context), Enabled.Get(context));
        }
    }
}
