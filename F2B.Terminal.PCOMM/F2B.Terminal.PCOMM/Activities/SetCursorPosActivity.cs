using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Terminal.PCOMM
{
    [Designer(typeof(PcommCanvasFieldsActivityDesigner))]
    [DisplayName("Set Cursor Pos")]
    [Description("Set the cursor position on the terminal screen.")]
    public sealed class SetCursorPosActivity : PcommDelayActivityBase
    {
        public SetCursorPosActivity() : base("Set Cursor Pos") {}

        [DisplayName("Session")]
        [Description("Connected PCOMM presentation space session.")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<PcommSession> Session { get; set; }

        [DisplayName("Row Index")]
        [Description("Cursor row index on the terminal screen.")]
        [RequiredArgument]
        [Category("Input.B")]
        public InArgument<int> Y { get; set; }

        [DisplayName("Column Index")]
        [Description("Cursor column index on the terminal screen.")]
        [RequiredArgument]
        [Category("Input.C")]
        public InArgument<int> X { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ApplyDelayBefore(context);

            var session = Session.Get(context);
            if (session == null)
            {
                throw new ArgumentNullException(nameof(Session), "Session is required.");
            }

            session.SetCursorPos(X.Get(context), Y.Get(context));
        }
    }
}
