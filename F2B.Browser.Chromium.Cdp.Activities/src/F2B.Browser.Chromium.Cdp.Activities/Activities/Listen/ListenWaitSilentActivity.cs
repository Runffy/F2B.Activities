using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [ToolboxPath("Listen")]
    [DisplayName("WaitSilent")]
    [Description("Wait until in-flight network requests drop to the given limit.")]
    [Designer(typeof(CdpEmptyActivityDesigner))]
    public sealed class ListenWaitSilentActivity : CodeActivity
    {
        public ListenWaitSilentActivity()
        {
            DisplayName = "WaitSilent";
        }


        [DisplayName("Target")]
        [Description("CdpTab or CdpFrame.")]
        [Category("Input.A")]
        [RequiredArgument]
        public InArgument<CdpBase> Target { get; set; }


        [DisplayName("Timeout (ms)")]
        [Description("Maximum wait time in milliseconds. ≤0 waits forever.")]
        [Category("Input.Z")]
        [DefaultValue(0)]
        public InArgument<int> Timeout { get; set; } = 0;


        [DisplayName("Targets Only")]
        [Description("When true, only waits for packets that matched the current URL/method/type filters.")]
        [Category("Input.B")]
        [DefaultValue(false)]
        public InArgument<bool> TargetsOnly { get; set; } = false;


        [DisplayName("Limit")]
        [Description("Treat as silent when this many in-flight requests remain. Default 0.")]
        [Category("Input.B")]
        [DefaultValue(0)]
        public InArgument<int> Limit { get; set; } = 0;


        [DisplayName("Throw On Timeout")]
        [DefaultValue(true)]
        [Category("Input.Z")]
        public InArgument<bool> ThrowOnTimeout { get; set; } = true;


        [DisplayName("Success")]
        [Category("Output")]
        public OutArgument<bool> Success { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var listener = CdpListenActivityHelper.GetListener(Target, context);
            var timeoutMs = CdpActivityArgumentHelper.GetOrDefault(Timeout, context, 0);
            var targetsOnly = CdpActivityArgumentHelper.GetOrDefault(TargetsOnly, context, false);
            var limit = CdpActivityArgumentHelper.GetOrDefault(Limit, context, 0);
            var throwOnTimeout = CdpActivityArgumentHelper.GetOrDefault(ThrowOnTimeout, context, true);
            var success = listener.WaitSilent(timeoutMs, targetsOnly, limit, throwOnTimeout);
            if (Success != null)
            {
                Success.Set(context, success);
            }
        }
    }
}
