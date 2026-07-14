using System.Activities;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Element-Click")]
    [Description("Click the target element.")]
    public sealed class ElementClickActivity : CdpElementTargetActivityBase
    {
        public ElementClickActivity()
            : base("Element-Click")
        {
        }

        [DisplayName("Button")]
        [Description("Mouse button used for the click.")]
        [Category("Input.D")]
        [DefaultValue(CdpMouseButton.Left)]
        [TypeConverter(typeof(CdpMouseButtonConverter))]
        public CdpMouseButton Button { get; set; } = CdpMouseButton.Left;

        [DisplayName("Method")]
        [Description("Interaction method used for the click.")]
        [Category("Input.D")]
        [DefaultValue(CdpInteractionMethod.Simulate)]
        [TypeConverter(typeof(CdpInteractionMethodConverter))]
        public CdpInteractionMethod Method { get; set; } = CdpInteractionMethod.Simulate;

        [DisplayName("Count")]
        [Description("Number of clicks.")]
        [Category("Input.D")]
        [DefaultValue(1)]
        public InArgument<int> Count { get; set; } = 1;

        [DisplayName("Timeout (ms)")]
        [Description("Timeout in milliseconds for locating the target element.")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            var element = ResolveTargetElementWithTimeout(context, Timeout);
            var count = CdpActivityArgumentHelper.GetOrDefault(Count, context, 1);
            element.Click(Button, Method, count);
        }
    }
}
