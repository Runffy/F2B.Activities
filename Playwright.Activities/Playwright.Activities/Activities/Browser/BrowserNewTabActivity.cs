using System.Activities;
using System.ComponentModel;

namespace Playwright.Activities
{
    [Designer(typeof(CanvasFieldsActivityDesigner))]
    [DisplayName("Browser.NewTab")]
    public sealed class BrowserNewTabActivity : CodeActivity
    {
        [RequiredArgument]
        [Category("Input")]
        public InArgument<PwBrowser> Browser { get; set; }

        [Category("Input")]
        public InArgument<string> Url { get; set; }

        [Category("Input")]
        [DefaultValue(15000)]
        public InArgument<int?> Timeout { get; set; } = 15000;

        [Category("Output")]
        public OutArgument<PwTab> Tab { get; set; }

        [Category("Output")]
        public OutArgument<TabInfo> TabInfo { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var tab = Browser.Get(context).NewTab(
                url: Url == null ? null : Url.Get(context),
                timeout: Timeout == null ? null : (double?)Timeout.Get(context));
            Tab?.Set(context, tab);
            TabInfo?.Set(context, tab?.GetInfo());
        }
    }
}
