using System;
using System.Activities;
using System.ComponentModel;
using System.Windows;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(BindEventDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Bind Event")]
    [Description("Design-time bind under AsyncForm BindEvents. For controls created at runtime, use Dynamic Bind Event.")]
    public sealed class BindEventActivity : NativeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public BindEventActivity()
        {
            DisplayName = "Bind Event";
            UiBehavior = new InArgument<string>("NoLock");
        }

        [RequiredArgument]
        [DisplayName("Control Id")]
        [Description("Control id from the form JSON (e.g. button1). Form close uses AsyncForm Close Scope.")]
        [Category("Input.A")]
        public InArgument<string> ControlId { get; set; }

        [RequiredArgument]
        [DisplayName("Control Type")]
        [Description("Bindable control type. Auto-filled when picking an id from Form JSON; required when typing id manually.")]
        [Category("Input.A")]
        public InArgument<string> ControlType { get; set; }

        [RequiredArgument]
        [DisplayName("Event Name")]
        [Description("Control event name, e.g. Click, Change, TextChanged.")]
        [Category("Input.A")]
        public InArgument<string> EventName { get; set; }

        [DisplayName("UI Behavior")]
        [Description("NoLock | LockQueue | LockIgnore. Controls UI while this handler runs.")]
        [Category("Input.B")]
        public InArgument<string> UiBehavior { get; set; }

        [Browsable(false)]
        public Activity Handler { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new BindEventActivity
            {
                DisplayName = "Bind Event",
                UiBehavior = new InArgument<string>("NoLock"),
                Handler = new System.Activities.Statements.Sequence { DisplayName = "Handler" }
            };
        }

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            var controlIdArg = new RuntimeArgument("ControlId", typeof(string), ArgumentDirection.In);
            metadata.Bind(ControlId, controlIdArg);
            metadata.AddArgument(controlIdArg);

            var controlTypeArg = new RuntimeArgument("ControlType", typeof(string), ArgumentDirection.In);
            metadata.Bind(ControlType, controlTypeArg);
            metadata.AddArgument(controlTypeArg);

            var eventNameArg = new RuntimeArgument("EventName", typeof(string), ArgumentDirection.In);
            metadata.Bind(EventName, eventNameArg);
            metadata.AddArgument(eventNameArg);

            var uiBehaviorArg = new RuntimeArgument("UiBehavior", typeof(string), ArgumentDirection.In);
            metadata.Bind(UiBehavior, uiBehaviorArg);
            metadata.AddArgument(uiBehaviorArg);

            if (Handler != null)
            {
                metadata.AddChild(Handler);
            }
        }

        protected override void Execute(NativeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);

            if (session.IsRegistering)
            {
                string controlId = ControlId.Get(context);
                string eventName = EventName.Get(context);
                UiBehavior behavior = FormSessionAccess.ParseUiBehavior(UiBehavior.Get(context));
                session.RegisterBinding(controlId, eventName, behavior, this);
                return;
            }

            if (Handler != null)
            {
                context.ScheduleActivity(Handler);
            }
        }
    }
}
