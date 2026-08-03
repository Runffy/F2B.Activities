using System.Activities;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Element-Check")]
    [Description("Check or uncheck the target element.")]
    public sealed class ElementCheckActivity : CdpElementTargetActivityBase
    {
        public ElementCheckActivity()
            : base("Element-Check")
        {
        }

        [DisplayName("Uncheck")]
        [Description("When true, unchecks the element instead of checking it.")]
        [Category("Input.D")]
        [DefaultValue(false)]
        public InArgument<bool> Uncheck { get; set; } = false;

        [DisplayName("Method")]
        [Description("Interaction method used for checking.")]
        [Category("Input.D")]
        [DefaultValue(CdpInteractionMethod.Simulate)]
        [TypeConverter(typeof(CdpInteractionMethodConverter))]
        public CdpInteractionMethod Method { get; set; } = CdpInteractionMethod.Simulate;

        [DisplayName("Timeout (ms)")]
        [Description("Timeout in milliseconds for locating the target element.")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            var element = ResolveTargetElementWithTimeout(context, Timeout);
            var uncheck = CdpActivityArgumentHelper.GetOrDefault(Uncheck, context, false);
            element.Check(uncheck, Method);
        }
    }
}
