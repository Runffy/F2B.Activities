using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [ToolboxPath("Listen")]
    [DisplayName("WaitExtraInfo")]
    [Description("Wait until extra request/response info is attached to a captured packet.")]
    [Designer(typeof(CdpEmptyActivityDesigner))]
    public sealed class ListenWaitExtraInfoActivity : CodeActivity
    {
        public ListenWaitExtraInfoActivity()
        {
            DisplayName = "WaitExtraInfo";
        }


        [DisplayName("Packet")]
        [Category("Input.A")]
        [RequiredArgument]
        public InArgument<CdpNetworkPacket> Packet { get; set; }


        [DisplayName("Timeout (ms)")]
        [Description("Maximum wait time in milliseconds. ≤0 waits forever.")]
        [Category("Input.Z")]
        [DefaultValue(0)]
        public InArgument<int> Timeout { get; set; } = 0;


        [DisplayName("Throw On Timeout")]
        [DefaultValue(true)]
        [Category("Input.Z")]
        public InArgument<bool> ThrowOnTimeout { get; set; } = true;


        [DisplayName("Success")]
        [Category("Output")]
        public OutArgument<bool> Success { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var packet = Packet == null ? null : Packet.Get(context);
            if (packet == null)
            {
                throw new System.InvalidOperationException("Packet is required.");
            }

            var timeoutMs = CdpActivityArgumentHelper.GetOrDefault(Timeout, context, 0);
            var throwOnTimeout = CdpActivityArgumentHelper.GetOrDefault(ThrowOnTimeout, context, true);
            var success = packet.WaitExtraInfo(timeoutMs, throwOnTimeout);
            if (Success != null)
            {
                Success.Set(context, success);
            }
        }
    }
}
