using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore.COM
{
    [DisplayName("Set Attribute")]
    [Description("Set attribute/property value on target element.")]
    public sealed class SetElementAttributeActivity : IeElementActivityBase
    {
        [Category("Input")]
        [DisplayName("Name")]
        [RequiredArgument]
        public InArgument<string> Name { get; set; }

        [Category("Input")]
        [DisplayName("Value")]
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
            ResolveWindow(context).set_attribute(
                locator: ResolveSelector(context),
                name: Name == null ? null : Name.Get(context),
                value: Value == null ? null : Value.Get(context),
                frame_path: ResolveFramePath(context),
                timeout: ResolveTimeout(context),
                trigger_events: ActivityArgumentHelper.GetOrDefault(TriggerEvents, context, true),
                delay_before: ActivityArgumentHelper.GetOrDefault(DelayBefore, context, 300),
                delay_after: ActivityArgumentHelper.GetOrDefault(DelayAfter, context, 0));
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);
            ValidateTextArgumentExpression(metadata, Name, "Name is required.");
        }
    }
}
