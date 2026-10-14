using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [ToolboxPath("Listen")]
    [DisplayName("Wait")]
    [Description("Wait until the requested number of captured packets is available. Always returns an array.")]
    [Designer(typeof(CdpEmptyActivityDesigner))]
    public sealed class ListenWaitActivity : CodeActivity
    {
        public ListenWaitActivity()
        {
            DisplayName = "Wait";
        }


        [DisplayName("Target")]
        [Description("CdpTab or CdpFrame.")]
        [Category("Input.A")]
        [RequiredArgument]
        public InArgument<CdpBase> Target { get; set; }


        [DisplayName("Count")]
        [Description("Number of packets to wait for.")]
        [Category("Input.B")]
        [DefaultValue(1)]
        public InArgument<int> Count { get; set; } = 1;


        [DisplayName("Timeout (ms)")]
        [Description("Maximum wait time in milliseconds. ≤0 waits forever.")]
        [Category("Input.Z")]
        [DefaultValue(0)]
        public InArgument<int> Timeout { get; set; } = 0;


        [DisplayName("Throw On Timeout")]
        [Description("When true, timeout throws. When false, returns an empty array and does not dequeue.")]
        [Category("Input.Z")]
        [DefaultValue(true)]
        public InArgument<bool> ThrowOnTimeout { get; set; } = true;


        [DisplayName("Packets")]
        [Description("Captured packets.")]
        [Category("Output")]
        public OutArgument<CdpNetworkPacket[]> Packets { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var listener = CdpListenActivityHelper.GetListener(Target, context);
            var count = CdpActivityArgumentHelper.GetOrDefault(Count, context, 1);
            var timeoutMs = CdpActivityArgumentHelper.GetOrDefault(Timeout, context, 0);
            var throwOnTimeout = CdpActivityArgumentHelper.GetOrDefault(ThrowOnTimeout, context, true);
            var packets = listener.Wait(count, timeoutMs, throwOnTimeout);
            if (Packets != null)
            {
                Packets.Set(context, packets);
            }
        }
    }
}
