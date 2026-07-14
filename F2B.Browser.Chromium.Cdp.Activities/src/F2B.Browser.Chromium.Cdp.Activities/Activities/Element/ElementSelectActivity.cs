using System.Activities;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Element-Select")]
    [Description("Select options in a select element.")]
    public sealed class ElementSelectActivity : CdpElementTargetActivityBase
    {
        public ElementSelectActivity()
            : base("Element-Select")
        {
        }

        [DisplayName("By")]
        [Description("Selection criteria.")]
        [Category("Input.D")]
        [DefaultValue(CdpActivitySelectBy.Text)]
        [TypeConverter(typeof(CdpActivitySelectByConverter))]
        public CdpActivitySelectBy By { get; set; } = CdpActivitySelectBy.Text;

        [DisplayName("Value")]
        [Description("Values to select.")]
        [RequiredArgument]
        [Category("Input.D")]
        public InArgument<object[]> Value { get; set; }

        [DisplayName("Timeout (ms)")]
        [Description("Timeout in milliseconds for locating the target element.")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            var element = ResolveTargetElementWithTimeout(context, Timeout);
            var values = Value.Get(context);
            CdpSelectHelper.Select(element, By, values);
        }
    }
}
