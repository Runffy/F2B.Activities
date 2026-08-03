using System.Activities;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Element-Style")]
    [Description("Get, set, or remove a CSS style on the target element.")]
    public sealed class ElementStyleActivity : CdpElementTargetActivityBase
    {
        public ElementStyleActivity()
            : base("Element-Style")
        {
        }

        [DisplayName("Name")]
        [Description("CSS style property name.")]
        [RequiredArgument]
        [Category("Input.D")]
        public InArgument<string> Name { get; set; }

        [DisplayName("Type")]
        [Description("Style operation type.")]
        [Category("Input.D")]
        [DefaultValue(CdpStyleOperationType.Get)]
        [TypeConverter(typeof(CdpStyleOperationTypeConverter))]
        public CdpStyleOperationType Type { get; set; } = CdpStyleOperationType.Get;

        [DisplayName("Set Value")]
        [Description("Style value used when Type is Set.")]
        [Category("Input.E")]
        public InArgument<string> SetValue { get; set; }

        [DisplayName("Output Result")]
        [Description("Style value returned when Type is Get.")]
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
                case CdpStyleOperationType.Set:
                    element.SetStyle(name, SetValue == null ? null : SetValue.Get(context));
                    break;
                case CdpStyleOperationType.Remove:
                    element.RunJs("this.style.removeProperty('" + name.Replace("'", "\\'") + "');");
                    break;
                default:
                    OutputResult?.Set(context, element.Style(name));
                    break;
            }
        }
    }
}
