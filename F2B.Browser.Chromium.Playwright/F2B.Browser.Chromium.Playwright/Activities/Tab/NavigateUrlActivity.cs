using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Navigate Url")]
    [Description("Navigate the tab to the specified URL.")]
    [Designer(typeof(CanvasFieldsActivityDesigner))]
    public sealed class NavigateUrlActivity : CodeActivity
    {
        public NavigateUrlActivity()
        {
            DisplayName = "Navigate Url";
        }

        [DisplayName("Input Tab")]
        [Description("Tab instance to navigate.")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<PwTab> Tab { get; set; }

        [DisplayName("Url")]
        [Description("Target URL to visit.")]
        [RequiredArgument]
        [Category("Input.B")]
        public InArgument<string> Url { get; set; }

        [DisplayName("Timeout (ms)")]
        [Description("Navigation timeout in milliseconds.")]
        [Category("Input.Z")]
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
