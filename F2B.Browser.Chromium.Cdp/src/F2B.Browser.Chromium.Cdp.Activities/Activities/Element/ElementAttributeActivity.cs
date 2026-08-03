using System.Activities;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Element-Attribute")]
    [Description("Get, set, or remove an attribute on the target element.")]
    public sealed class ElementAttributeActivity : CdpElementTargetActivityBase
    {
        public ElementAttributeActivity()
            : base("Element-Attribute")
        {
        }

        [DisplayName("Name")]
        [Description("Attribute name.")]
        [RequiredArgument]
        [Category("Input.D")]
        public InArgument<string> Name { get; set; }

        [DisplayName("Type")]
        [Description("Attribute operation type.")]
        [Category("Input.D")]
        [DefaultValue(CdpAttributeOperationType.Get)]
        [TypeConverter(typeof(CdpAttributeOperationTypeConverter))]
        public CdpAttributeOperationType Type { get; set; } = CdpAttributeOperationType.Get;

        [DisplayName("Set Value")]
        [Description("Attribute value used when Type is Set.")]
        [Category("Input.E")]
        public InArgument<string> SetValue { get; set; }

        [DisplayName("Output Result")]
        [Description("Attribute value returned when Type is Get.")]
        [Category("Output")]
        public OutArgument<string> OutputResult { get; set; }

        [DisplayName("Timeout (ms)")]
        [Description("Timeout in milliseconds for locating the target element.")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            var element = ResolveTargetElementWithTimeout(context, Timeout);
            var name = Name.Get(context);

            switch (Type)
            {
                case CdpAttributeOperationType.Set:
                    element.SetAttr(name, SetValue == null ? null : SetValue.Get(context));
                    break;
                case CdpAttributeOperationType.Remove:
                    element.RemoveAttr(name);
                    break;
                default:
                    OutputResult?.Set(context, element.Attr(name));
                    break;
            }
        }
    }
}
