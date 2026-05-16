using System.Activities;
using System.ComponentModel;

namespace Playwright.Activities
{
    [Designer(typeof(CanvasFieldsActivityDesigner))]
    [DisplayName("Tab.NavigateUrl")]
    public sealed class TabNavigateUrlActivity : CodeActivity
    {
        [RequiredArgument]
        [Category("Input")]
        public InArgument<PwTab> Tab { get; set; }

        [RequiredArgument]
        [Category("Input")]
        public InArgument<string> Url { get; set; }

        [Category("Input")]
        [DefaultValue(15000)]
        public InArgument<int?> Timeout { get; set; } = 15000;

        protected override void Execute(CodeActivityContext context)
        {
            Tab.Get(context).NavigateUrl(
                Url.Get(context),
                Timeout == null ? null : (double?)Timeout.Get(context));
        }
    }
}
