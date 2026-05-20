using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.IExplore
{
    [DisplayName("Get Attribute")]
    [Description("Get an HTML attribute from the target element.")]
    public sealed class GetAttributeActivity : ElementTargetActivityBase
    {
        [DisplayName("Attribute Name")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<string> AttributeName { get; set; }

        [DisplayName("Attribute Value")]
        [Category("Output")]
        public OutArgument<string> AttributeValue { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var name = AttributeName.Get(context);
            var window = GetWindow(context);
            var timeout = ActivityArgumentHelper.GetOrDefault(Timeout, context, OperationDefaults.TimeoutMs);
            string value;

            if (TargetType == IeElementTargetType.Element)
                value = window.GetAttribute(ResolveTargetElement(context), name, timeout);
            else
                value = window.GetAttribute(ResolveLocator(context), name, timeout);

            AttributeValue?.Set(context, value);
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);
            if (AttributeName == null || AttributeName.Expression == null)
                metadata.AddValidationError("Attribute Name is required.");
        }
    }
}
