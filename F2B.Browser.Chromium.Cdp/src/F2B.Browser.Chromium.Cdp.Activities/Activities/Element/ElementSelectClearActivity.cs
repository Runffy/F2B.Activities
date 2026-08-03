using System.Activities;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Element-Select-Clear")]
    [Description("Unselect all options in a multi-select element.")]
    public sealed class ElementSelectClearActivity : CdpElementTargetActivityBase
    {
        public ElementSelectClearActivity()
            : base("Element-Select-Clear")
        {
        }

        [DisplayName("Timeout (ms)")]
        [Description("Timeout in milliseconds for locating the target element.")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            var element = ResolveTargetElementWithTimeout(context, Timeout);
            element.UnselectAll();
        }
    }
}
