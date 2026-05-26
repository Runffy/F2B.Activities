using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Terminal.PCOMM
{
    [Designer(typeof(PcommCanvasFieldsActivityDesigner))]
    [DisplayName("Input Text")]
    [Description("Move the cursor to the specified position and send text to the terminal.")]
    public sealed class InputTextActivity : PcommDelayActivityBase
    {
        public InputTextActivity() : base("Input Text") {}

        [DisplayName("Session")]
        [Description("Connected PCOMM presentation space session.")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<PcommSession> Session { get; set; }

        [DisplayName("Text")]
        [Description("Text to send to the terminal.")]
        [RequiredArgument]
        [Category("Input.C")]
        public InArgument<string> Text { get; set; }

        [DisplayName("X")]
        [Description("Cursor X coordinate.")]
        [RequiredArgument]
        [Category("Input.B")]
        public InArgument<int> X { get; set; }

        [DisplayName("Y")]
        [Description("Cursor Y coordinate.")]
        [RequiredArgument]
        [Category("Input.B")]
        public InArgument<int> Y { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ApplyDelayBefore(context);

            var session = Session.Get(context);
            if (session == null)
            {
                throw new ArgumentNullException(nameof(Session), "Session is required.");
            }

            var text = Text.Get(context);
            session.InputText(X.Get(context), Y.Get(context), text);
        }
    }
}
