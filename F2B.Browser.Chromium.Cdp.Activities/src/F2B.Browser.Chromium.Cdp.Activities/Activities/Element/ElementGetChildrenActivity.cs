using System;
using System.Activities;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Element-GetChildren")]
    [Description("Get child elements under Target. Child Selector filters children (direct by default; Deepdive searches descendants). Target must be a CdpElement.")]
    [TypeDescriptionProvider(typeof(ElementGetChildrenTypeDescriptionProvider))]
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

        [DisplayName("Deepdive")]
        [Description("When true, search all descendants matching Child Selector. When false, match direct children only.")]
        [Category("Input.C")]
        [DefaultValue(false)]
        [TypeConverter(typeof(CdpBooleanTypeConverter))]
        public bool Deepdive { get; set; }

        [DisplayName("Timeout (ms)")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        [DisplayName("Children")]
        [Category("Output")]
        public OutArgument<CdpElement[]> Children { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var target = CdpTargetResolver.GetRoot(Target, context, "Target") as CdpElement;
            if (target == null)
            {
                throw new InvalidOperationException("Target is required and must be a CdpElement.");
            }

            var delayBefore = CdpActivityArgumentHelper.GetOrDefault(DelayBefore, context, 300);
            CdpDelay.Apply(delayBefore);

            // Selector is a child filter, not a re-resolve of Target (unlike other Element-* activities).
            var childSelector = Selector == null ? null : Selector.Get(context);
            Children?.Set(context, target.Children(childSelector, Deepdive));
        }
    }
}
