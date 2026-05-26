using System.Activities;
using System.ComponentModel;

namespace F2B.Terminal.PCOMM
{
    public abstract class PcommDelayActivityBase : CodeActivity
    {
        [DisplayName("Delay Before")]
        [Description("Wait time in milliseconds before execution.")]
        [Category("Input.Z.Time")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        protected void ApplyDelayBefore(CodeActivityContext context)
        {
            PcommActivityHelper.ApplyDelayBefore(DelayBefore, context);
        }
    }
}
