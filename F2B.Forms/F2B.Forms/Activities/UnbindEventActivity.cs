using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Unbind Event")]
    [Description("Unregister a previously bound control event. Leave Event Name empty to unbind all events for that control.")]
    public sealed class UnbindEventActivity : CodeActivity
    {
        public UnbindEventActivity()
        {
            DisplayName = "Unbind Event";
        }

        [RequiredArgument]
        [DisplayName("Control Id")]
        [Category("Input.A")]
        public InArgument<string> ControlId { get; set; }

        [DisplayName("Event Name")]
        [Description("Event to unbind, e.g. Click. Empty = unbind all events on this control.")]
        [Category("Input.A")]
        public InArgument<string> EventName { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            session.UnregisterBindings(ControlId.Get(context), EventName.Get(context));
        }
    }
}
