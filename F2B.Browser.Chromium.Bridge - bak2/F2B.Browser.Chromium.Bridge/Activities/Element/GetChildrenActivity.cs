using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Get Children")]
    [Description("Get matching child elements under the target element.")]
    [TypeDescriptionProvider(typeof(BridgeElementTargetTypeDescriptionProvider))]
    public sealed class GetChildrenActivity : BridgeElementTargetActivityBase
    {
        public GetChildrenActivity() : base("Get Children") { }

        [DisplayName("Child Selector")]
        [RequiredArgument]
        [Category("Input.D")]
        public InArgument<string> ChildSelector { get; set; }

        [DisplayName("Deepdive")]
        [Category("Input.E")]
        [DefaultValue(false)]
        [TypeConverter("F2B.Browser.Chromium.Bridge.BridgeBooleanTypeConverter, F2B.Browser.Chromium.Bridge")]
        public bool Deepdive { get; set; }

        [DisplayName("Children")]
        [Category("Output")]
        public OutArgument<BwElement[]> Children { get; set; }

        [DisplayName("Timeout (ms)")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            Children?.Set(context, ResolveTargetElementWithTimeout(context, Timeout).GetChildren(
                ChildSelector.Get(context),
                Deepdive,
                BridgeActivityArgumentHelper.GetOrDefault(Timeout, context, 15000)));
        }
    }
}
