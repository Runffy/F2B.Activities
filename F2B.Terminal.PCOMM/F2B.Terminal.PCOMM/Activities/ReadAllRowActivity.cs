using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Terminal.PCOMM
{
    [Designer(typeof(PcommCanvasFieldsActivityDesigner))]
    [DisplayName("Read All Row")]
    [Description("Read all terminal rows and join them with line breaks.")]
    public sealed class ReadAllRowActivity : PcommDelayActivityBase
    {
        [DisplayName("Session")]
        [Description("Connected PCOMM presentation space session.")]
        [RequiredArgument]
        [Category("Input.A.Window")]
        public InArgument<PcommSession> Session { get; set; }

        [DisplayName("Content")]
        [Description("All rows joined by line breaks.")]
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

            Content?.Set(context, session.ReadAllRows());
        }
    }
}
