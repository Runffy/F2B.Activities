using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Terminal.PCOMM
{
    [Designer(typeof(PcommCanvasFieldsActivityDesigner))]
    [DisplayName("Wait For Text In Row")]
    [Description("Wait until the specified text appears in a row within the column range.")]
    public sealed class WaitForTextInRowActivity : PcommDelayActivityBase
    {
        public WaitForTextInRowActivity() : base("Wait For Text In Row") {}

        [DisplayName("Session")]
        [Description("Connected PCOMM presentation space session.")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<PcommSession> Session { get; set; }

        [DisplayName("Text")]
        [Description("Text to wait for in the specified row.")]
        [RequiredArgument]
        [Category("Input.B")]
        public InArgument<string> Text { get; set; }

        [DisplayName("Row")]
        [Description("Row number on the terminal screen.")]
        [RequiredArgument]
        [Category("Input.C")]
        public InArgument<int> Row { get; set; }

        [DisplayName("Start Col")]
        [Description("Start column. When empty, defaults to 1.")]
        [Category("Input.D")]
        public InArgument<int?> StartCol { get; set; }

        [DisplayName("End Col")]
        [Description("End column. When empty, defaults to the session column count.")]
        [Category("Input.D")]
        public InArgument<int?> EndCol { get; set; }

        [DisplayName("Timeout (ms)")]
        [Description("Maximum time to wait for the text.")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        [DisplayName("Interval (ms)")]
        [Description("Polling interval passed to WaitForStringInRect.")]
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
            var row = Row.Get(context);
            int? startCol = StartCol == null || StartCol.Expression == null ? null : StartCol.Get(context);
            int? endCol = EndCol == null || EndCol.Expression == null ? null : EndCol.Get(context);
            var timeout = Timeout == null || Timeout.Expression == null ? 15000 : Timeout.Get(context);
            var interval = Interval == null || Interval.Expression == null ? 500 : Interval.Get(context);

            var found = session.WaitForTextInRow(text, row, startCol, endCol, timeout, interval);
            Result?.Set(context, found);
        }
    }
}
