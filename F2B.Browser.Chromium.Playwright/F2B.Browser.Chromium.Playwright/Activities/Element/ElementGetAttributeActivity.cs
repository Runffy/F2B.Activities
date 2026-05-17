using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Get Element Attribute")]
    [Description("Get a specified attribute value from the target element.")]
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    public sealed class ElementGetAttributeActivity : ElementTargetActivityBase
    {
        [DisplayName("Name")]
        [Description("Attribute name to read.")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<string> Name { get; set; }

        [DisplayName("Value")]
        [Description("Outputs the attribute value.")]
        [Category("Output")]
        public OutArgument<string> Value { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Value?.Set(context, ResolveTargetElement(context).GetAttribute(Name.Get(context)));
        }
    }
}
