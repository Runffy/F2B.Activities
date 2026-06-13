using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Set Attribute")]
    [Description("Set an attribute value on the target element.")]
    [TypeDescriptionProvider(typeof(BridgeElementTargetTypeDescriptionProvider))]
    public sealed class SetAttributeActivity : BridgeElementTargetActivityBase
    {
        public SetAttributeActivity() : base("Set Attribute") { }

        [DisplayName("Name")]
        [RequiredArgument]
        [Category("Input.D")]
        public InArgument<string> Name { get; set; }

        [DisplayName("Value")]
        [RequiredArgument]
        [Category("Input.D")]
        public InArgument<string> Value { get; set; }

        [DisplayName("Timeout (ms)")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            ResolveTargetElementWithTimeout(context, Timeout).SetAttribute(
                Name.Get(context),
                Value.Get(context),
                BridgeActivityArgumentHelper.GetOrDefault(Timeout, context, 15000));
        }
    }
}
