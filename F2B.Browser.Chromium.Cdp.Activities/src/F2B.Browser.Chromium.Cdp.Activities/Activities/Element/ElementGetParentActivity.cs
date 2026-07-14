using System;
using System.Activities;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Element-GetParent")]
    [Description("Get the parent element. Target must be a CdpElement.")]
    public sealed class ElementGetParentActivity : CdpElementTargetActivityBase
    {
        public ElementGetParentActivity()
            : base("Element-GetParent")
        {
        }

        protected override bool RequireCdpElementTarget
        {
            get { return true; }
        }

        [DisplayName("Level")]
        [Category("Input.D")]
        [DefaultValue(1)]
        public InArgument<int> Level { get; set; } = 1;

        [DisplayName("Timeout (ms)")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        [DisplayName("Parent")]
        [Category("Output")]
        public OutArgument<CdpElement> Parent { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var level = CdpActivityArgumentHelper.GetOrDefault(Level, context, 1);
            var timeoutMs = CdpActivityArgumentHelper.GetOrDefault(Timeout, context, 15000);
            var element = ResolveTargetElement(context, timeoutMs);
            var selector = string.Format("<parent level=\"{0}\" />", Math.Max(1, level));
            var parent = element.FindElement(selector, timeoutMs, true);
            CdpActivityArgumentHelper.SetCdpElement(Parent, context, parent);
        }
    }
}
