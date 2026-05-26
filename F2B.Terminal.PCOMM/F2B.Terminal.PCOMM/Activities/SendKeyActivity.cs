using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Terminal.PCOMM
{
    [Designer(typeof(PcommCanvasFieldsActivityDesigner))]
    [DisplayName("Send Key")]
    [Description("Send a predefined key to the terminal session.")]
    public sealed class SendKeyActivity : PcommDelayActivityBase
    {
        public SendKeyActivity() : base("Send Key") {}

        [DisplayName("Session")]
        [Description("Connected PCOMM presentation space session.")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<PcommSession> Session { get; set; }

        [DisplayName("Key")]
        [Description("Key to send to the terminal.")]
        [Category("Input.B")]
        [DefaultValue(PcommKey.Enter)]
        [TypeConverter("F2B.Terminal.PCOMM.PcommKeyTypeConverter, F2B.Terminal.PCOMM")]
        public PcommKey Key { get; set; } = PcommKey.Enter;

        protected override void Execute(CodeActivityContext context)
        {
            ApplyDelayBefore(context);

            var session = Session.Get(context);
            if (session == null)
            {
                throw new ArgumentNullException(nameof(Session), "Session is required.");
            }

            session.SendKey(Key);
        }
    }
}
