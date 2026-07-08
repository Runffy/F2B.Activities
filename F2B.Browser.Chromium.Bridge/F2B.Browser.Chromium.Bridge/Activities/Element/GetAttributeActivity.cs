using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Get Attribute")]
    [Description("Get an HTML attribute, DOM property, or known alias (tag, outerHTML, innerHTML, disabled, checked, value, text, etc.) from the target element.")]
    [TypeDescriptionProvider(typeof(BridgeElementTargetTypeDescriptionProvider))]
    public sealed class GetAttributeActivity : BridgeElementTargetActivityBase
    {
        public GetAttributeActivity() : base("Get Attribute") { }

        [DisplayName("Name")]
        [RequiredArgument]
        [Category("Input.D")]
        public InArgument<string> Name { get; set; }

        [DisplayName("Value")]
        [Category("Output")]
        public OutArgument<string> Value { get; set; }

        [DisplayName("Timeout (ms)")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            Value?.Set(context, ResolveTargetElementWithTimeout(context, Timeout).GetAttribute(
                Name.Get(context),
                BridgeActivityArgumentHelper.GetOrDefault(Timeout, context, 15000)));
        }
    }
}
