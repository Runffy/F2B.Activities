using System.Activities;
using System.ComponentModel;

namespace F2B.Terminal.PCOMM
{
    [Designer(typeof(PcommCanvasFieldsActivityDesigner))]
    [DisplayName("Close Terminal")]
    [Description("Gracefully close all open PCOMM session windows via UI automation.")]
    public sealed class CloseTerminalActivity : CodeActivity
    {
        public CloseTerminalActivity()
        {
            DisplayName = "Close Terminal";
        }

        [DisplayName("Timeout (ms)")]
        [Description("Maximum time to wait while closing all session windows.")]
        [Category("Input")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;






        [DisplayName("Delay After (ms)")]
        [Description("Delay after each close attempt to allow PCOMM to shut down cleanly.")]
        [Category("Input")]
        [DefaultValue(2000)]
        public InArgument<int> DelayAfter { get; set; } = 2000;





        [DisplayName("Closed Count")]
        [Description("Number of session windows closed.")]
        [Category("Output")]
        public OutArgument<int> ClosedCount { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var timeout = Timeout == null || Timeout.Expression == null ? 15000 : Timeout.Get(context);
            var delayAfter = DelayAfter == null || DelayAfter.Expression == null ? 2000 : DelayAfter.Get(context);

            var closedCount = PcommSessionCloser.SoftCloseAllSessions(timeout, delayAfter);
            ClosedCount?.Set(context, closedCount);
        }
    }
}
