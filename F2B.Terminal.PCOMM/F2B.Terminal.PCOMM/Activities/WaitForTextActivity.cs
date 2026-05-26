using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Terminal.PCOMM
{
    [Designer(typeof(PcommCanvasFieldsActivityDesigner))]
    [DisplayName("Wait For Text")]
    [Description("Wait until the specified text appears on the terminal screen.")]
    public sealed class WaitForTextActivity : PcommDelayActivityBase
    {
        public WaitForTextActivity() : base("Wait For Text") {}

        [DisplayName("Session")]
        [Description("Connected PCOMM presentation space session.")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<PcommSession> Session { get; set; }

        [DisplayName("Text")]
        [Description("Text to wait for on the terminal screen.")]
        [RequiredArgument]
        [Category("Input.B")]
        public InArgument<string> Text { get; set; }

        [DisplayName("Timeout (ms)")]
        [Description("Maximum time to wait for the text.")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        [DisplayName("Interval (ms)")]
        [Description("Polling interval passed to WaitForString.")]
        [Category("Input.Z")]
        [DefaultValue(500)]
        public InArgument<int> Interval { get; set; } = 500;

        [DisplayName("Result")]
        [Description("Returns true when the text is found.")]
        [Category("Output")]
        public OutArgument<bool> Result { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ApplyDelayBefore(context);

            var session = Session.Get(context);
            if (session == null)
            {
                throw new ArgumentNullException(nameof(Session), "Session is required.");
            }

            var text = Text.Get(context);
            var timeout = Timeout == null || Timeout.Expression == null ? 15000 : Timeout.Get(context);
            var interval = Interval == null || Interval.Expression == null ? 500 : Interval.Get(context);

            var found = session.WaitForText(text, timeout, interval);
            Result?.Set(context, found);
        }
    }
}
