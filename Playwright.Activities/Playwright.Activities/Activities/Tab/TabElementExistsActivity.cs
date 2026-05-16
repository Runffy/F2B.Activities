using System.Activities;
using System.ComponentModel;

namespace Playwright.Activities
{
    [Designer(typeof(CanvasFieldsActivityDesigner))]
    [DisplayName("Tab.ElementExists")]
    public sealed class TabElementExistsActivity : CodeActivity
    {
        [RequiredArgument]
        [Category("Input")]
        public InArgument<PwTab> Tab { get; set; }

        [RequiredArgument]
        [Category("Input")]
        public InArgument<string> Selector { get; set; }

        [Category("Input")]
        [DefaultValue(0)]
        public InArgument<int> Index { get; set; } = 0;

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
