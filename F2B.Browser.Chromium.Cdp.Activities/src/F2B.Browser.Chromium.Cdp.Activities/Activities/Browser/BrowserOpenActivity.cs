using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Browser-Open")]
    [Description("打开或附着到 Chromium 浏览器（Open + Attach 合一，无需单独的 Browser-Attach）。相同 Port 且相同 User Data Dir 的浏览器已在运行时直接附着；否则启动新实例。仅 Port 或 User Data Dir 冲突时：Force=true 结束旧进程后重启，Force=false 报错。")]
    [Designer(typeof(CdpEmptyActivityDesigner))]
    public sealed class BrowserOpenActivity : CodeActivity
    {
        public BrowserOpenActivity()
        {
            DisplayName = "Browser-Open";
        }

        [DisplayName("Port")]
        [Description("CDP 调试端口。≤0 表示自动分配。与已运行浏览器 Port 一致且 User Data Dir 匹配时将附着，而非新开。")]
        [Category("Input.A")]
        public InArgument<int?> Port { get; set; }

        [DisplayName("User Data Dir")]
        [Description("用户数据目录，或特殊值：system、temp、documents。与已运行浏览器一致且 Port 匹配时将附着。")]
        [Category("Input.B")]
        public InArgument<string> UserDataDir { get; set; }

        [DisplayName("Executable Path")]
        [Description("浏览器可执行文件路径，或特殊值：chrome、edge。仅在新开浏览器时使用。")]
        [Category("Input.B")]
        public InArgument<string> ExecutablePath { get; set; }

        [DisplayName("Start Arguments")]
        [Description("额外启动参数。仅在新开浏览器时使用。")]
        [Category("Input.B")]
        public InArgument<string> StartArguments { get; set; }

        [DisplayName("Force")]
        [Description("Port 被占用且 User Data Dir 不一致，或被非 CDP 进程占用时：true=结束旧进程后新开，false=抛出异常。相同 Port+User Data Dir 已存在 CDP 浏览器时无需 Force，直接附着。")]
        [Category("Input.B")]
        [DefaultValue(false)]
        public InArgument<bool> Force { get; set; } = false;

        [DisplayName("Url")]
        [Description("可选。新启动时导航到已有空白标签页（避免多留一个「新标签页」）；附着已有浏览器时 NewTab 打开。不提供则返回 LatestTab。")]
        [Category("Input.C")]
        public InArgument<string> Url { get; set; }

        [DisplayName("Browser")]
        [Description("输出的浏览器实例（可能是新启动的，也可能是附着到已有实例）。")]
        [Category("Output")]
        public OutArgument<CdpBrowser> Browser { get; set; }

        [DisplayName("Tab")]
        [Description("输出的标签页。有 Url 时为新建标签；无 Url 时为 LatestTab。")]
        [Category("Output")]
        public OutArgument<CdpTab> Tab { get; set; }

        [DisplayName("Attached")]
        [Description("true 表示附着到已有浏览器；false 表示本次新启动了浏览器进程。")]
        [Category("Output")]
        public OutArgument<bool> Attached { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var portValue = Port == null || Port.Expression == null ? 0 : (Port.Get(context) ?? 0);
            var options = new BrowserOpenOptions
            {
                Port = portValue,
                Force = CdpActivityArgumentHelper.GetOrDefault(Force, context, false),
                UserDataDir = UserDataDir == null ? null : UserDataDir.Get(context),
                ExecutablePath = ExecutablePath == null ? null : ExecutablePath.Get(context),
                StartArguments = StartArguments == null ? null : StartArguments.Get(context)
            };

            var browser = ChromiumBrowser.OpenBrowser(options);
            var url = Url == null ? null : Url.Get(context);

            CdpTab tab;
            if (!string.IsNullOrWhiteSpace(url))
            {
                // Fresh launch already opens one blank tab (chrome://newtab). Reuse it so
                // Browser-Open does not leave an orphan 「新标签页」 next to the test page.
                if (!browser.AttachedToExisting && browser.TabsCount > 0)
                {
                    tab = browser.LatestTab;
                    tab.Navigate(url);
                }
                else
                {
                    tab = browser.NewTab(url);
                }
            }
            else
            {
                tab = browser.LatestTab;
            }

            Browser?.Set(context, browser);
            Tab?.Set(context, tab);
            Attached?.Set(context, browser.AttachedToExisting);
        }
    }
}
