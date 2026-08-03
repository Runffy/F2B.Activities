using System.Activities;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Element-Property")]
    [Description("Get or set a DOM property on the target element.")]
    public sealed class ElementPropertyActivity : CdpElementTargetActivityBase
    {
        public ElementPropertyActivity()
            : base("Element-Property")
        {
        }

        [DisplayName("Name")]
        [Description("DOM property name, such as value or outerHTML.")]
        [RequiredArgument]
        [Category("Input.D")]
        public InArgument<string> Name { get; set; }

        [DisplayName("Type")]
        [Description("Property operation type.")]
        [Category("Input.D")]
        [DefaultValue(CdpPropertyOperationType.Get)]
        [TypeConverter(typeof(CdpPropertyOperationTypeConverter))]
        public CdpPropertyOperationType Type { get; set; } = CdpPropertyOperationType.Get;

        [DisplayName("Set Value")]
        [Description("Property value used when Type is Set.")]
        [Category("Input.E")]
        public InArgument<string> SetValue { get; set; }

        [DisplayName("Output Result")]
        [Description("Property value returned when Type is Get.")]
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

            if (Type == CdpPropertyOperationType.Set)
            {
                element.SetProperty(name, SetValue == null ? null : SetValue.Get(context));
                return;
            }

            OutputResult?.Set(context, element.Property(name));
        }
    }
}
