using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Open Browser")]
    [Description("Open a new Chromium window via Bridge extension and output the latest tab.")]
    public sealed class BrowserOpenActivity : CodeActivity
    {
        public BrowserOpenActivity()
        {
            DisplayName = "Open Browser";
        }

        [DisplayName("Url")]
        [Description("Initial page URL for the new browser window.")]
        [Category("Input")]
        public InArgument<string> Url { get; set; }

        [DisplayName("Connect Timeout (ms)")]
        [Description("Maximum wait time for the Bridge extension to connect.")]
        [Category("Input")]
        [DefaultValue(60000)]
        public InArgument<int> ConnectTimeout { get; set; } = 60000;

        [DisplayName("Open Timeout (ms)")]
        [Description("Maximum wait time for the new window/tab to finish loading.")]
        [Category("Input")]
        [DefaultValue(15000)]
        public InArgument<int> OpenTimeout { get; set; } = 15000;

        [DisplayName("Extension Instance Id")]
        [Description("Optional. Use when multiple Bridge extensions are connected.")]
        [Category("Input")]
        public InArgument<string> InstanceId { get; set; }

        [DisplayName("Chrome Executable Path")]
        [Description("Optional. Chrome/Edge path used for cold start when no browser is running.")]
        [Category("Input")]
        public InArgument<string> ChromeExecutablePath { get; set; }

        [DisplayName("Extension Path")]
        [Description("Optional. Unpacked extension folder passed as --load-extension on cold start. Leave empty when the extension is already installed in your default Chrome profile.")]
        [Category("Input")]
        public InArgument<string> ExtensionPath { get; set; }

        [DisplayName("Output Browser")]
        [Description("Outputs the Bridge browser instance.")]
        [Category("Output")]
        public OutArgument<BwBrowser> Browser { get; set; }

        [DisplayName("Output Tab")]
        [Description("Outputs the latest tab in the opened browser window.")]
        [Category("Output")]
        public OutArgument<BwTab> Tab { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var connectTimeoutMs = BridgeActivityArgumentHelper.GetOrDefault(ConnectTimeout, context, 60000);
            var openTimeoutMs = BridgeActivityArgumentHelper.GetOrDefault(OpenTimeout, context, 15000);
            var instanceId = InstanceId == null ? null : InstanceId.Get(context);
            var url = Url == null ? null : Url.Get(context);
            var chromeExecutablePath = ChromeExecutablePath == null ? null : ChromeExecutablePath.Get(context);
            var extensionPath = ExtensionPath == null ? null : ExtensionPath.Get(context);
            var connectTimeout = TimeSpan.FromMilliseconds(connectTimeoutMs);

            BridgeActivityServices.EnsureStarted();
            var launchedCold = false;
            if (!BridgeActivityServices.TryWaitForExtension(TimeSpan.FromSeconds(2), out _, instanceId)
                && !BridgeChromiumLauncher.IsChromiumProcessRunning())
            {
                launchedCold = BridgeChromiumLauncher.TryLaunch(chromeExecutablePath, extensionPath, url);
                if (!launchedCold)
                {
                    throw new InvalidOperationException(
                        "Could not start Chromium for cold start. Set Chrome Executable Path on Open Browser, "
                        + "or start Chrome manually with the F2B Bridge extension loaded.");
                }
            }

            var browser = BridgeActivityServices.GetBrowser(instanceId, connectTimeout);

            BwTab initialTab;
            BwTab latestTab;
            if (launchedCold && !string.IsNullOrWhiteSpace(url))
            {
                latestTab = browser.WaitForTabByUrl(url, openTimeoutMs);
                initialTab = latestTab;
            }
            else
            {
                browser.BrowserOpen(out initialTab, url, openTimeoutMs);
                latestTab = browser.GetLatestTab();
            }

            Browser?.Set(context, browser);
            Tab?.Set(context, latestTab);
        }
    }
}
