using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Open Browser")]
    [Description("Launch a browser and output the browser instance.")]
    [TypeDescriptionProvider(typeof(BrowserOpenTypeDescriptionProvider))]
    public sealed class BrowserOpenActivity : CodeActivity, IBrowserOpenConfig
    {
        [DisplayName("Headless")]
        [Description("Whether to launch the browser in headless mode.")]
        [Category("Input")]
        [DefaultValue(false)]
        [TypeConverter("F2B.Browser.Chromium.Playwright.BooleanTypeConverter, F2B.Browser.Chromium.Playwright")]
        public bool Headless { get; set; } = false;

        [Category("Input")]
        [DefaultValue(@"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe")]
        public InArgument<string> BrowserPath { get; set; } = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe";

        [DisplayName("Start Maximized")]
        [Description("Whether to maximize the window by default.")]
        [Category("Input")]
        [DefaultValue(true)]
        [TypeConverter("F2B.Browser.Chromium.Playwright.BooleanTypeConverter, F2B.Browser.Chromium.Playwright")]
        public bool StartMaximized { get; set; } = true;

        [DisplayName("Remote Debugging Port")]
        [Description(
            "Optional. Only when set, the browser gets --remote-debugging-port=<value>. When empty, no explicit port argument is added; Playwright still connects DevTools internally (typically --remote-debugging-pipe), which automation requires and cannot be fully disabled." +
            "Some sites (e.g. IE mode) may still warn about remote debugging.")]
        [Category("Input")]
        public InArgument<int?> RemoteDebuggingPort { get; set; }

        [DisplayName("Use System Dir")]
        [Description("Whether to use the system default user data directory.")]
        [Category("Input")]
        [DefaultValue(true)]
        [TypeConverter("F2B.Browser.Chromium.Playwright.BooleanTypeConverter, F2B.Browser.Chromium.Playwright")]
        public bool UseSystemDir
        {
            get => _useSystemDir;
            set
            {
                _useSystemDir = value;
                TypeDescriptor.Refresh(this);
            }
        }

        [DisplayName("User Data Dir")]
        [Description("Custom browser user data directory path.")]
        [Category("Input")]
        public InArgument<string> UserDataDir { get; set; }

        [DisplayName("Output Browser")]
        [Description("Outputs the opened browser instance.")]
        [Category("Output")]
        public OutArgument<PwBrowser> Browser { get; set; }

        [DisplayName("Output Tab")]
        [Description(
            "PwTab for the first browser page after WaitForLoadState(Load); shares the browser session—often avoids a separate Browser.GetLatestTab step.")]
        [Category("Output")]
        public OutArgument<PwTab> Tab { get; set; }

        private bool _useSystemDir = true;

        protected override void Execute(CodeActivityContext context)
        {
            var client = new PlaywrightSyncClient();

            var browser = client.BrowserOpen(
                out var initialTab,
                headless: Headless,
                browserPath: BrowserPath == null ? null : BrowserPath.Get(context),
                startMaximized: StartMaximized,
                remoteDebuggingPort: RemoteDebuggingPort == null ? null : RemoteDebuggingPort.Get(context),
                useSystemDir: UseSystemDir,
                userDataDir: UserDataDir == null ? null : UserDataDir.Get(context));
            Browser?.Set(context, browser);

            Tab?.Set(context, initialTab);
        }
    }
}
