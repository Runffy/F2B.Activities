using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Terminal.PCOMM
{
    [Designer(typeof(PcommCanvasFieldsActivityDesigner))]
    [DisplayName("Read Single Row")]
    [Description("Read text from a single terminal row within the specified column range.")]
    public sealed class ReadSingleRowActivity : PcommDelayActivityBase
    {
        [DisplayName("Session")]
        [Description("Connected PCOMM presentation space session.")]
        [RequiredArgument]
        [Category("Input.A.Window")]
        public InArgument<PcommSession> Session { get; set; }

        [DisplayName("Row")]
        [Description("Row number on the terminal screen.")]
        [RequiredArgument]
        [Category("Input.B.Location")]
        public InArgument<int> Row { get; set; }

        [DisplayName("Start Col")]
        [Description("Start column. When empty, defaults to 1.")]
        [Category("Input.C.Location")]
        public InArgument<int?> StartCol { get; set; }

        [DisplayName("End Col")]
        [Description("End column. When empty, defaults to the session column count.")]
        [Category("Input.C.Location")]
        public InArgument<int?> EndCol { get; set; }

        [DisplayName("Content")]
        [Description("Text read from the specified row and column range.")]
        [Category("Output")]
        public OutArgument<string> Content { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ApplyDelayBefore(context);

            var session = Session.Get(context);
            if (session == null)
            {
                throw new ArgumentNullException(nameof(Session), "Session is required.");
            }

            var row = Row.Get(context);
            int? startCol = StartCol == null || StartCol.Expression == null ? null : StartCol.Get(context);
            int? endCol = EndCol == null || EndCol.Expression == null ? null : EndCol.Get(context);

            var content = session.ReadSingleRow(row, startCol, endCol);
            Content?.Set(context, content ?? string.Empty);
        }
    }
}
