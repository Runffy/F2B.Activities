using System.Activities;
using System.ComponentModel;

namespace F2B.Terminal.PCOMM
{
    [Designer(typeof(PcommCanvasFieldsActivityDesigner))]
    [DisplayName("Connect to Terminal")]
    [Description("Connect to an IBM PCOMM terminal session and output the presentation space object.")]
    public sealed class ConnectToTerminalActivity : CodeActivity
    {
        [DisplayName("Session Name")]
        [Description("PCOMM session identifier, for example A, B, or C.")]
        [RequiredArgument]
        [Category("Input")]
        [DefaultValue("A")]
        public InArgument<string> SessionName { get; set; } = "A";

        [DisplayName("Session")]
        [Description("Outputs the connected PCOMM presentation space session.")]
        [Category("Output")]
        public OutArgument<PcommSession> Session { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var sessionName = SessionName == null || SessionName.Expression == null
                ? "A"
                : SessionName.Get(context);

            if (string.IsNullOrWhiteSpace(sessionName))
            {
                sessionName = "A";
            }

            var client = new PcommClient();
            var session = client.Connect(sessionName);
            Session?.Set(context, session);
        }
    }
}
