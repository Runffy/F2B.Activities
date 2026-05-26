using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Get Attribute")]
    [Description("Get a specified attribute value from the target element.")]
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    public sealed class GetAttributeActivity : ElementTargetActivityBase
    {
        public GetAttributeActivity() : base("Get Attribute") {}

        [DisplayName("Name")]
        [Description("Attribute name to read.")]
        [RequiredArgument]
        [Category("Input.D")]
        public InArgument<string> Name { get; set; }

        [DisplayName("Value")]
        [Description("Outputs the attribute value.")]
        [Category("Output")]
        public OutArgument<string> Value { get; set; }

        [DisplayName("Timeout (ms)")]
        [Description("Timeout in milliseconds for locating the target element.")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            Value?.Set(context, ResolveTargetElementWithTimeout(context, Timeout).GetAttribute(Name.Get(context)));
        }
    }
}
