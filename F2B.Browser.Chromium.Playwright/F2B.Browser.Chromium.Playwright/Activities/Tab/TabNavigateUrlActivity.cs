using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Navigate Tab Url")]
    [Description("Navigate the tab to the specified URL.")]
    [Designer(typeof(CanvasFieldsActivityDesigner))]
    public sealed class TabNavigateUrlActivity : CodeActivity
    {
        [DisplayName("Input Tab")]
        [Description("Tab instance to navigate.")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<PwTab> Tab { get; set; }

        [DisplayName("Url")]
        [Description("Target URL to visit.")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<string> Url { get; set; }

        [DisplayName("Timeout")]
        [Description("Navigation timeout in milliseconds.")]
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
