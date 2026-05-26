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
        [DisplayName("Session")]
        [Description("Connected PCOMM presentation space session.")]
        [RequiredArgument]
        [Category("Input.A.Window")]
        public InArgument<PcommSession> Session { get; set; }

        [DisplayName("X")]
        [Description("Cursor X coordinate.")]
        [RequiredArgument]
        [Category("Input.B.Location")]
        public InArgument<int> X { get; set; }

        [DisplayName("Y")]
        [Description("Cursor Y coordinate.")]
        [RequiredArgument]
        [Category("Input.B.Location")]
        public InArgument<int> Y { get; set; }

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
