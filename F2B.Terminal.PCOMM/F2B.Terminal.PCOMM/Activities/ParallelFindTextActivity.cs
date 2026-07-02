using System;
using System.Activities;
using System.Collections.Generic;
using System.ComponentModel;

namespace F2B.Terminal.PCOMM
{
    [Designer(typeof(PcommCanvasFieldsActivityDesigner))]
    [DisplayName("Parallel Find Text")]
    [Description("Poll the terminal screen and return the index of the first matching candidate text.")]
    public sealed class ParallelFindTextActivity : PcommDelayActivityBase
    {
        public ParallelFindTextActivity() : base("Parallel Find Text") { }

        [DisplayName("Session")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<PcommSession> Session { get; set; }

        [DisplayName("Texts")]
        [Description("Candidate texts to search for on the full screen.")]
        [RequiredArgument]
        [Category("Input.B")]
        public InArgument<IList<string>> Texts { get; set; }

        [DisplayName("Timeout (ms)")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        [DisplayName("Interval (ms)")]
        [Category("Input.Z")]
        [DefaultValue(500)]
        public InArgument<int> Interval { get; set; } = 500;

        [DisplayName("Matched Index")]
        [Category("Output")]
        public OutArgument<int> MatchedIndex { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ApplyDelayBefore(context);

            var session = Session.Get(context);
            if (session == null)
                throw new ArgumentNullException(nameof(Session), "Session is required.");

            var texts = Texts.Get(context);
            var timeout = PcommActivityHelper.GetOrDefault(Timeout, context, 15000);
            var interval = PcommActivityHelper.GetOrDefault(Interval, context, 500);

            MatchedIndex?.Set(context, session.ParallelFindText(texts, timeout, interval));
        }
    }
}
