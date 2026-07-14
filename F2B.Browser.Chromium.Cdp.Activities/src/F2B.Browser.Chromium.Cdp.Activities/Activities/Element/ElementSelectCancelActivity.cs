using System.Activities;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Element-Select-Cancel")]
    [Description("Unselect options in a multi-select element.")]
    public sealed class ElementSelectCancelActivity : CdpElementTargetActivityBase
    {
        public ElementSelectCancelActivity()
            : base("Element-Select-Cancel")
        {
        }

        [DisplayName("By")]
        [Description("Unselection criteria.")]
        [Category("Input.D")]
        [DefaultValue(CdpActivitySelectBy.Text)]
        [TypeConverter(typeof(CdpActivitySelectByConverter))]
        public CdpActivitySelectBy By { get; set; } = CdpActivitySelectBy.Text;

        [DisplayName("Value")]
        [Description("Values to unselect.")]
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
            CdpSelectHelper.Unselect(element, By, values);
        }
    }
}
