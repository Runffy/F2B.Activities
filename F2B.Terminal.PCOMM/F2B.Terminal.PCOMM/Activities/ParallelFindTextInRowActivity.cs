using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Terminal.PCOMM
{
    [Designer(typeof(PcommCanvasFieldsActivityDesigner))]
    [DisplayName("Parallel Find Text In Row")]
    [Description("Poll specified rows and return the index of the first matching row/text candidate.")]
    public sealed class ParallelFindTextInRowActivity : PcommDelayActivityBase
    {
        public ParallelFindTextInRowActivity() : base("Parallel Find Text In Row") { }

        [DisplayName("Session")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<PcommSession> Session { get; set; }

        [DisplayName("Candidates")]
        [Description("2D array of row/text pairs, e.g. New Object(,) {{1, \"text1\"}, {2, \"text2\"}}.")]
        [RequiredArgument]
        [Category("Input.B")]
        public InArgument<object> Candidates { get; set; }

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

            var candidates = Candidates.Get(context) as Array;
            if (candidates == null || candidates.Rank != 2 || candidates.GetLength(1) < 2)
                throw new ArgumentException("Candidates must be a 2D array with row/text pairs.", nameof(Candidates));

            var matrix = new object[candidates.GetLength(0), 2];
            for (var i = 0; i < candidates.GetLength(0); i++)
            {
                matrix[i, 0] = candidates.GetValue(i, 0);
                matrix[i, 1] = candidates.GetValue(i, 1);
            }

            var timeout = PcommActivityHelper.GetOrDefault(Timeout, context, 15000);
            var interval = PcommActivityHelper.GetOrDefault(Interval, context, 500);

            MatchedIndex?.Set(context, session.ParallelFindTextInRow(matrix, timeout, interval));
        }
    }
}
