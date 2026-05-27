using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore.COM
{
    [DisplayName("Get Attribute")]
    [Description("Get attribute/property value from target element.")]
    public sealed class GetElementAttributeActivity : IeElementActivityBase
    {
        [Category("Time")]
        [DisplayName("Delay Before")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        [Category("Input")]
        [DisplayName("Name")]
        [RequiredArgument]
        public InArgument<string> Name { get; set; }

        [Category("Input")]
        [DisplayName("Default Value")]
        public InArgument<object> DefaultValue { get; set; }

        [Category("Output")]
        [DisplayName("Value")]
        public OutArgument<object> Value { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ActivityArgumentHelper.ApplyDelayBefore(DelayBefore, context, 300);

            var result = ResolveWindow(context).get_element_attribute(
                locator: ResolveSelector(context),
                name: Name == null ? null : Name.Get(context),
                frame_path: ResolveFramePath(context),
                timeout: ResolveTimeout(context),
                default_value: DefaultValue == null ? null : DefaultValue.Get(context));

            Value.Set(context, result);
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);
            ValidateTextArgumentExpression(metadata, Name, "Name is required.");
        }
    }
}
