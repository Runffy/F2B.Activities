using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Set Attribute")]
    [Description("Set a specified attribute value on the target element.")]
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    public sealed class SetAttributeActivity : ElementTargetActivityBase
    {
        [DisplayName("Name")]
        [Description("Attribute name to set.")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<string> Name { get; set; }

        [DisplayName("Value")]
        [Description("Attribute value to write.")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<string> Value { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            ResolveTargetElement(context).SetAttribute(
                name: Name.Get(context),
                value: Value == null ? null : Value.Get(context));
        }
    }
}
