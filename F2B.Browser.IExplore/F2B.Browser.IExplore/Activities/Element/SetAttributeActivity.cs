using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore
{
    [DisplayName("Set Attribute")]
    [Description("Set an HTML attribute on the target element (e.g. id for mark-then-reuse). Empty value removes the attribute.")]
    public sealed class SetAttributeActivity : ElementTargetActivityBase
    {
        [DisplayName("Attribute Name")]
        [Description("Attribute to set, e.g. id, title, data-custom.")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<string> AttributeName { get; set; }

        [DisplayName("Attribute Value")]
        [Description("Value to set. Leave empty to remove the attribute.")]
        [Category("Input")]
        public InArgument<string> AttributeValue { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var name = AttributeName.Get(context);
            var value = AttributeValue == null ? null : AttributeValue.Get(context);
            var window = GetWindow(context);
            var timeout = ActivityArgumentHelper.GetOrDefault(Timeout, context, OperationDefaults.TimeoutMs);

            if (TargetType == IeElementTargetType.Element)
                window.SetAttribute(ResolveTargetElement(context), name, value ?? string.Empty, timeout);
            else
                window.SetAttribute(ResolveLocator(context), name, value ?? string.Empty, timeout);
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);
            if (AttributeName == null || AttributeName.Expression == null)
                metadata.AddValidationError("Attribute Name is required.");
        }
    }
}
