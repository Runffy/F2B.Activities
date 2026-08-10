using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Browser-ChangeWindowState")]
    [Description("Change the browser window state (maximize, minimize, or normal). Maximize/Normal also soft-activates the window to the foreground (not TopMost).")]
    [Designer(typeof(CdpCanvasFieldsActivityDesigner))]
    public sealed class BrowserChangeWindowStateActivity : CodeActivity
    {
        public BrowserChangeWindowStateActivity()
        {
            DisplayName = "Browser-ChangeWindowState";
        }

        [DisplayName("Port")]
        [Description("CDP port used to attach when Browser/Tab is not provided.")]
        [Category("Input.A")]
        public InArgument<int?> Port { get; set; }

        [DisplayName("Browser")]
        [Description("Browser instance. Ignored when Tab is provided (Tab.Browser is used).")]
        [Category("Input.A")]
        public InArgument<CdpBrowser> Browser { get; set; }

        [DisplayName("Window State")]
        [Description("Target window state. Maximize and Normal bring the browser to the foreground (soft activate). Minimize does not.")]
        [Category("Input.B")]
        [DefaultValue(CdpBrowserWindowStateOption.Maximize)]
        [TypeConverter(typeof(CdpBrowserWindowStateOptionConverter))]
        public CdpBrowserWindowStateOption WindowState { get; set; } = CdpBrowserWindowStateOption.Maximize;

        [DisplayName("Tab")]
        [Description("Optional tab used to identify the target window. When set, that tab's window is changed (and the tab is activated). Defaults to the latest tab of Browser/Port.")]
        [Category("Input.C")]
        public InArgument<CdpTab> Tab { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var port = Port == null ? null : Port.Get(context);
            var browserArg = Browser == null ? null : Browser.Get(context);
            var tab = Tab == null ? null : Tab.Get(context);

            // Tab identifies the window: always use the tab's own browser connection when present.
            CdpBrowser browser;
            if (tab != null && tab.Browser != null)
            {
                browser = tab.Browser;
            }
            else
            {
                browser = CdpBrowserLocator.Resolve(browserArg, port);
            }

            if (tab != null)
            {
                try
                {
                    browser.ActivateTab(tab);
                }
                catch
                {
                    // Best effort — window bounds can still be resolved by target id.
                }
            }

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
