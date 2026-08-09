using System;
using System.Activities;
using System.ComponentModel;
using System.Windows;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(BindEventDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Dynamic Bind Event")]
    [Description("Bind a control event at runtime (e.g. after Create Control). Place in Init Scope or any handler; the Handler body runs when the event fires.")]
    public sealed class DynamicBindEventActivity : NativeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public DynamicBindEventActivity()
        {
            DisplayName = "Dynamic Bind Event";
            UiBehavior = new InArgument<string>("NoLock");
        }

        [RequiredArgument]
        [DisplayName("Control Id")]
        [Description("Target control id (must already exist when this activity runs).")]
        [Category("Input.A")]
        public InArgument<string> ControlId { get; set; }

        [RequiredArgument]
        [DisplayName("Control Type")]
        [Description("Bindable control type used to pick Event Name in the designer.")]
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
            return new DynamicBindEventActivity
            {
                DisplayName = "Dynamic Bind Event",
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
            string controlId = ControlId.Get(context);
            string eventName = EventName.Get(context);

            // AsyncForm schedules this activity again when the bound event fires.
            if (IsDispatchingThisBinding(session, controlId, eventName))
            {
                if (Handler != null)
                {
                    context.ScheduleActivity(Handler);
                }

                return;
            }

            UiBehavior behavior = FormSessionAccess.ParseUiBehavior(UiBehavior.Get(context));
            session.RegisterBinding(controlId, eventName, behavior, this, replaceExisting: true);
        }

        private static bool IsDispatchingThisBinding(FormSession session, string controlId, string eventName)
        {
            if (session == null || !session.IsHandlerRunning)
            {
                return false;
            }

            return string.Equals(
                    session.LastControlId,
                    controlId == null ? null : controlId.Trim(),
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    session.LastEventName,
                    eventName == null ? null : eventName.Trim(),
                    StringComparison.OrdinalIgnoreCase);
        }
    }
}
