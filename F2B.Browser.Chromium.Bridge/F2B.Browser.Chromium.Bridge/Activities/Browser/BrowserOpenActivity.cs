using System;
using System.Activities;
using System.ComponentModel;
using System.Diagnostics;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Open Browser")]
    [Description("Launch a new Chromium window via command line and bind the Bridge extension to it.")]
    public sealed class BrowserOpenActivity : CodeActivity
    {
        public BrowserOpenActivity()
        {
            DisplayName = "Open Browser";
        }

        [DisplayName("Url")]
        [Description("Optional initial page URL for the new window.")]
        [Category("Input")]
        public InArgument<string> Url { get; set; }

        [DisplayName("Connect Timeout (ms)")]
        [Description("Maximum wait time for the Bridge extension to connect after launch.")]
        [Category("Input")]
        [DefaultValue(60000)]
        public InArgument<int> ConnectTimeout { get; set; } = 60000;

        [DisplayName("Window Detect Timeout (ms)")]
        [Description("Safety timeout while the extension registers the new window in Chrome. Does not wait for page load or URL.")]
        [Category("Input")]
        [DefaultValue(5000)]
        public InArgument<int> OpenTimeout { get; set; } = 5000;

        [DisplayName("Extension Instance Id")]
        [Description("Optional. Use when multiple Bridge extensions are connected.")]
        [Category("Input")]
        public InArgument<string> InstanceId { get; set; }

        [DisplayName("Chrome Executable Path")]
        [Description("Optional. Path to chrome.exe or msedge.exe. When empty, a installed Chrome/Edge is resolved automatically.")]
        [Category("Input")]
        public InArgument<string> ChromeExecutablePath { get; set; }

        [DisplayName("Launch Arguments")]
        [Description("Optional Chromium flags, e.g. --user-data-dir=C:\\Profile --incognito or Edge --inprivate.")]
        [Category("Input")]
        public InArgument<string> LaunchArguments { get; set; }

        [DisplayName("Extension Path")]
        [Description("Optional unpacked extension folder for --load-extension. Leave empty when the extension is already installed in the profile.")]
        [Category("Input")]
        public InArgument<string> ExtensionPath { get; set; }

        [DisplayName("Output Browser")]
        [Description("Outputs the Bridge browser instance bound to the new window.")]
        [Category("Output")]
        public OutArgument<BwBrowser> Browser { get; set; }

        [DisplayName("Output Tab")]
        [Description("Outputs a tab from the newly opened window.")]
        [Category("Output")]
        public OutArgument<BwTab> Tab { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var connectTimeoutMs = BridgeActivityArgumentHelper.GetOrDefault(ConnectTimeout, context, 60000);
            var windowDetectTimeoutMs = BridgeActivityArgumentHelper.GetOrDefault(OpenTimeout, context, 5000);
            var instanceId = InstanceId == null ? null : InstanceId.Get(context);
            var url = Url == null ? null : Url.Get(context);
            var chromeExecutablePath = ChromeExecutablePath == null ? null : ChromeExecutablePath.Get(context);
            var launchArguments = LaunchArguments == null ? null : LaunchArguments.Get(context);
            var extensionPath = ExtensionPath == null ? null : ExtensionPath.Get(context);
            var connectTimeout = TimeSpan.FromMilliseconds(connectTimeoutMs);

            var total = Stopwatch.StartNew();
            BridgeActivityServices.EnsureStarted();

            var extensionOnline = BridgeActivityServices.IsExtensionConnected(instanceId);
            BridgeDiagnostics.Trace(
                "OpenBrowser: start url="
                + (string.IsNullOrWhiteSpace(url) ? "(none)" : url)
                + " windowDetectMs="
                + windowDetectTimeoutMs
                + " extensionOnline="
                + extensionOnline
                + " +"
                + total.ElapsedMilliseconds
                + "ms");

            var launchSw = Stopwatch.StartNew();
            BridgeChromiumLauncher.LaunchNewWindow(
                chromeExecutablePath,
                url,
                launchArguments,
                extensionPath);
            BridgeDiagnostics.Trace(
                "OpenBrowser: launch +"
                + launchSw.ElapsedMilliseconds
                + "ms (total +"
                + total.ElapsedMilliseconds
                + "ms)");

            var connectSw = Stopwatch.StartNew();
            var postLaunchConnectTimeout = extensionOnline
                ? TimeSpan.FromMilliseconds(Math.Min(connectTimeoutMs, 5000))
                : connectTimeout;

            if (!extensionOnline)
            {
                BridgeDiagnostics.Trace(
                    "OpenBrowser: extension offline before launch; waiting up to "
                    + connectTimeoutMs
                    + "ms for WebSocket connect");
            }

            var browser = BridgeActivityServices.GetBrowser(instanceId, postLaunchConnectTimeout);
            BridgeDiagnostics.Trace(
                "OpenBrowser: connect +"
                + connectSw.ElapsedMilliseconds
                + "ms (total +"
                + total.ElapsedMilliseconds
                + "ms)");

            var resolveSw = Stopwatch.StartNew();
            var tab = browser.ResolveNewWindowTab(null, windowDetectTimeoutMs);
            BridgeDiagnostics.Trace(
                "OpenBrowser: resolve windowId="
                + browser.WindowId
                + " tabId="
                + (tab == null ? 0 : tab.TabId)
                + " +"
                + resolveSw.ElapsedMilliseconds
                + "ms (total +"
                + total.ElapsedMilliseconds
                + "ms)");

            Browser?.Set(context, browser);
            Tab?.Set(context, tab);
        }
    }
}
