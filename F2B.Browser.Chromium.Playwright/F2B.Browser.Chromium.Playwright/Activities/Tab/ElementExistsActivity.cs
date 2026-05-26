using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Element Exists")]
    [Description("Check whether a matching element exists in the tab.")]
    [Designer(typeof(CanvasFieldsActivityDesigner))]
    public sealed class ElementExistsActivity : CodeActivity
    {
        public ElementExistsActivity()
        {
            DisplayName = "Element Exists";
        }

        [DisplayName("Input Tab")]
        [Description("Tab instance to check.")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<PwTab> Tab { get; set; }

        [DisplayName("Selector")]
        [Description("Selector used to locate the target element.")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<string> Selector { get; set; }

        [DisplayName("Index")]
        [Description("Index to use when multiple elements match (0-based).")]
        [Category("Input.B")]
        [DefaultValue(0)]
        public InArgument<int> Index { get; set; } = 0;

        [DisplayName("Exists")]
        [Description("Outputs whether the element exists.")]
        [Category("Output")]
        public OutArgument<bool> Exists { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var exists = Tab.Get(context).ElementExists(
                Selector.Get(context),
                ActivityArgumentHelper.GetOrDefault(Index, context, 0));
            Exists?.Set(context, exists);
        }
    }
}
