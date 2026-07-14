using System.Activities;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Element-GetChildren")]
    [Description("Get direct child elements. Target must be a CdpElement.")]
    public sealed class ElementGetChildrenActivity : CdpElementTargetActivityBase
    {
        public ElementGetChildrenActivity()
            : base("Element-GetChildren")
        {
        }

        protected override bool RequireCdpElementTarget
        {
            get { return true; }
        }

        [DisplayName("Timeout (ms)")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        [DisplayName("Children")]
        [Category("Output")]
        public OutArgument<CdpElement[]> Children { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var element = ResolveTargetElementWithTimeout(context, Timeout);
            Children?.Set(context, element.Children());
        }
    }
}
