using System.Activities;
using System.ComponentModel;

namespace Playwright.Activities
{
    [DisplayName("Browser.Open")]
    [TypeDescriptionProvider(typeof(BrowserOpenTypeDescriptionProvider))]
    public sealed class BrowserOpenActivity : CodeActivity, IBrowserOpenConfig
    {
        [Category("Input")]
        [DefaultValue(false)]
        [TypeConverter("Playwright.Activities.BooleanTypeConverter, Playwright.Activities")]
        public bool Headless { get; set; } = false;

        [Category("Input")]
        [DefaultValue(@"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe")]
        public InArgument<string> BrowserPath { get; set; } = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe";

        [Category("Input")]
        [DefaultValue(true)]
        [TypeConverter("Playwright.Activities.BooleanTypeConverter, Playwright.Activities")]
        public bool StartMaximized { get; set; } = true;

        [Category("Input")]
        public InArgument<int?> RemoteDebuggingPort { get; set; }

        [Category("Input")]
        [DefaultValue(true)]
        [TypeConverter("Playwright.Activities.BooleanTypeConverter, Playwright.Activities")]
        public bool UseSystemDir
        {
            get => _useSystemDir;
            set
            {
                _useSystemDir = value;
                TypeDescriptor.Refresh(this);
            }
        }

        [Category("Input")]
        public InArgument<string> UserDataDir { get; set; }

        [Category("Output")]
        public OutArgument<PwBrowser> Browser { get; set; }

        private bool _useSystemDir = true;

        protected override void Execute(CodeActivityContext context)
        {
            var client = new PlaywrightSyncClient();

            var browser = client.BrowserOpen(
                headless: Headless,
                browserPath: BrowserPath == null ? null : BrowserPath.Get(context),
                startMaximized: StartMaximized,
                remoteDebuggingPort: RemoteDebuggingPort == null ? null : RemoteDebuggingPort.Get(context),
                useSystemDir: UseSystemDir,
                userDataDir: UserDataDir == null ? null : UserDataDir.Get(context));

            Browser?.Set(context, browser);
        }
    }
}
