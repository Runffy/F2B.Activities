using System.Activities;
using System.ComponentModel;

namespace F2B.Terminal.PCOMM
{
    public abstract class PcommDelayActivityBase : CodeActivity
    {
        protected PcommDelayActivityBase(string displayName)
        {
            DisplayName = displayName;
        }

        [DisplayName("Delay Before (ms)")]
        [Description("Wait time in milliseconds before execution.")]
        [Category("Input.Z")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        protected void ApplyDelayBefore(CodeActivityContext context)
        {
            PcommActivityHelper.ApplyDelayBefore(DelayBefore, context);
        }
    }
}
