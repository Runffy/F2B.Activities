using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore.COM
{
    [DisplayName("Press Keys")]
    [Description("Send key(s) to target element, supports single key or combo.")]
    public sealed class PressKeysActivity : IeElementActivityBase
    {
        [Category("Input")]
        [DisplayName("Keys")]
        [RequiredArgument]
        public InArgument<object> Keys { get; set; }

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
            ResolveWindow(context).press_keys(
                locator: ResolveSelector(context),
                keys: Keys == null ? null : Keys.Get(context),
                frame_path: ResolveFramePath(context),
                timeout: ResolveTimeout(context),
                delay_before: ActivityArgumentHelper.GetOrDefault(DelayBefore, context, 300),
                delay_after: ActivityArgumentHelper.GetOrDefault(DelayAfter, context, 0));
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);
            ValidateArgumentExpression(metadata, Keys, "Keys is required.");
        }
    }
}
