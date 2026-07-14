using System.Activities;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Element-Click-ForNewTab")]
    [Description("Click the target element and wait for a new tab to open.")]
    public sealed class ElementClickForNewTabActivity : CdpElementTargetActivityBase
    {
        public ElementClickForNewTabActivity()
            : base("Element-Click-ForNewTab")
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
        [Description("Timeout in milliseconds for locating the element and waiting for the new tab.")]
        [Category("Input.Z")]
        [DefaultValue(3000)]
        public InArgument<int> Timeout { get; set; } = 3000;

        [DisplayName("Tab")]
        [Description("Outputs the newly opened tab.")]
        [Category("Output")]
        public OutArgument<CdpTab> Tab { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var timeoutMs = CdpActivityArgumentHelper.GetOrDefault(Timeout, context, 3000);
            var element = ResolveTargetElement(context, timeoutMs);
            var count = CdpActivityArgumentHelper.GetOrDefault(Count, context, 1);
            var tab = element.ClickForNewTab(Button, Method, count, timeoutMs);
            Tab?.Set(context, tab);
        }
    }
}
