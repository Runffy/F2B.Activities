using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Get Parent")]
    [Description("Get a parent element at the specified level.")]
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    public sealed class GetParentActivity : ElementTargetActivityBase
    {
        [DisplayName("Level")]
        [Description("Number of levels to move up.")]
        [Category("Input")]
        [DefaultValue(1)]
        public InArgument<int> Level { get; set; } = 1;

        [DisplayName("Parent")]
        [Description("Outputs the located parent element.")]
        [Category("Output")]
        public OutArgument<PwElement> Parent { get; set; }

        [DisplayName("Timeout (ms)")]
        [Description("Timeout in milliseconds for locating the target element.")]
        [Category("Input")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            var parent = ResolveTargetElementWithTimeout(context, Timeout)
                .GetParent(ActivityArgumentHelper.GetOrDefault(Level, context, 1));
            ActivityArgumentHelper.SetPwElement(Parent, context, parent);
        }
    }
}
