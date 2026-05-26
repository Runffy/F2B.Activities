using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("New Tab")]
    [Description("Create a new tab in the browser and return it.")]
    [Designer(typeof(CanvasFieldsActivityDesigner))]
    public sealed class NewTabActivity : CodeActivity
    {
        public NewTabActivity()
        {
            DisplayName = "New Tab";
        }

        [DisplayName("Input Browser")]
        [Description("Browser instance used to create the tab.")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<PwBrowser> Browser { get; set; }

        [DisplayName("Url")]
        [Description("Target URL to open in the new tab.")]
        [Category("Input.B")]
        public InArgument<string> Url { get; set; }

        [DisplayName("Timeout (ms)")]
        [Description("Timeout in milliseconds for tab creation and navigation.")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int?> Timeout { get; set; } = 15000;

        [DisplayName("Tab")]
        [Description("Outputs the created tab instance.")]
        [Category("Output")]
        public OutArgument<PwTab> Tab { get; set; }

        [DisplayName("Tab Info")]
        [Description("Outputs basic information of the new tab.")]
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
