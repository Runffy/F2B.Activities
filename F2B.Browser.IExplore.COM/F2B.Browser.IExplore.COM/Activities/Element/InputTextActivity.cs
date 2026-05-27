using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore.COM
{
    [DisplayName("Input Text")]
    [Description("Input text into target element.")]
    public sealed class InputTextActivity : IeElementActivityBase
    {
        [Category("Input")]
        [DisplayName("Value")]
        [RequiredArgument]
        public InArgument<object> Value { get; set; }

        [Category("Input")]
        [DisplayName("Trigger Events")]
        [DefaultValue(true)]
        public InArgument<bool> TriggerEvents { get; set; } = true;

        [Category("Time")]
        [DisplayName("Delay Before")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        [Category("Time")]
        [DisplayName("Delay After")]
        [DefaultValue(0)]
        public InArgument<int> DelayAfter { get; set; } = 0;

        protected override void Execute(CodeActivityContext context)
        {
            ResolveWindow(context).input_text(
                locator: ResolveSelector(context),
                value: Value.Get(context),
                frame_path: ResolveFramePath(context),
                timeout: ResolveTimeout(context),
                trigger_events: ActivityArgumentHelper.GetOrDefault(TriggerEvents, context, true),
                delay_before: ActivityArgumentHelper.GetOrDefault(DelayBefore, context, 300),
                delay_after: ActivityArgumentHelper.GetOrDefault(DelayAfter, context, 0));
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);
            ValidateArgumentExpression(metadata, Value, "Value is required.");
        }
    }
}
