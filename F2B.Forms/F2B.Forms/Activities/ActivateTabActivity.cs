using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Activate Tab")]
    [Description("Activate a TabControl tab by TabPage control Id (e.g. tabPage1).")]
    public sealed class ActivateTabActivity : CodeActivity
    {
        public ActivateTabActivity()
        {
            DisplayName = "Activate Tab";
        }

        [RequiredArgument]
        [DisplayName("Tab Page Id")]
        [Description("Id of the TabPage to activate (not the TabControl Id).")]
        [Category("Input.A")]
        public InArgument<string> TabPageId { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            session.ActivateTab(TabPageId.Get(context));
        }
    }
}
