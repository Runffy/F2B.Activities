using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Browser-ChangeWindowState")]
    [Description("Change the browser window state (maximize, minimize, or normal).")]
    [Designer(typeof(CdpCanvasFieldsActivityDesigner))]
    public sealed class BrowserChangeWindowStateActivity : CodeActivity
    {
        public BrowserChangeWindowStateActivity()
        {
            DisplayName = "Browser-ChangeWindowState";
        }

        [DisplayName("Port")]
        [Description("CDP port used to attach when Browser is not provided.")]
        [Category("Input.A")]
        public InArgument<int?> Port { get; set; }

        [DisplayName("Browser")]
        [Description("Browser instance.")]
        [Category("Input.A")]
        public InArgument<CdpBrowser> Browser { get; set; }

        [DisplayName("Window State")]
        [Description("Target window state.")]
        [Category("Input.B")]
        [DefaultValue(CdpBrowserWindowStateOption.Maximize)]
        [TypeConverter(typeof(CdpBrowserWindowStateOptionConverter))]
        public CdpBrowserWindowStateOption WindowState { get; set; } = CdpBrowserWindowStateOption.Maximize;

        [DisplayName("Tab")]
        [Description("Optional tab used to identify the target window. Defaults to the latest tab.")]
        [Category("Input.C")]
        public InArgument<CdpTab> Tab { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var port = Port == null ? null : Port.Get(context);
            var browserArg = Browser == null ? null : Browser.Get(context);
            var browser = CdpBrowserLocator.Resolve(browserArg, port);
            var tab = Tab == null ? null : Tab.Get(context);

            switch (WindowState)
            {
                case CdpBrowserWindowStateOption.Minimize:
                    browser.Minimize(tab);
                    break;
                case CdpBrowserWindowStateOption.Normal:
                    browser.Normal(tab);
                    break;
                default:
                    browser.Maximize(tab);
                    break;
            }
        }
    }
}
