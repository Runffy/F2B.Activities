using System.Activities;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Element-Select-Get")]
    [Description("Get currently selected options from a select element.")]
    public sealed class ElementSelectGetActivity : CdpElementTargetActivityBase
    {
        public ElementSelectGetActivity()
            : base("Element-Select-Get")
        {
        }

        [DisplayName("Selected Options")]
        [Description("Outputs the selected options.")]
        [Category("Output")]
        public OutArgument<CdpSelectedOption[]> SelectedOptions { get; set; }

        [DisplayName("Timeout (ms)")]
        [Description("Timeout in milliseconds for locating the target element.")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            var element = ResolveTargetElementWithTimeout(context, Timeout);
            var options = CdpSelectHelper.GetSelectedOptions(element);
            SelectedOptions?.Set(context, options);
        }
    }
}
