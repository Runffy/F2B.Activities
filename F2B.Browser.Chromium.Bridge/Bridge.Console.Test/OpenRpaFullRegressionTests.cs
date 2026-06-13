using System;
using System.IO;
using System.Linq;
using System.Threading;
using F2B.Browser.Chromium.Bridge;

namespace F2B.Bridge.ConsoleTest
{
    /// <summary>
    /// 1:1 镜像 bridge-full-regression.xaml（file:// 协议），覆盖 OpenRPA workflow 全部 Activity 场景。
    /// </summary>
    internal sealed class OpenRpaFullRegressionTests
    {
        private readonly BridgeHost _host;
        private readonly BwBrowser _browser;
        private readonly string _fileDemoUrl;
        private readonly string _fileNavAUrl;
        private readonly string _fileNavBUrl;
        private readonly string _testOutputDir;
        private readonly Action<string, Action> _runTest;

        private BwTab _demoTab;

        public OpenRpaFullRegressionTests(
            BridgeHost host,
            BwBrowser browser,
            string fileDemoUrl,
            string fileNavAUrl,
            string fileNavBUrl,
            string testOutputDir,
            Action<string, Action> runTest)
        {
            _host = host;
            _browser = browser;
            _fileDemoUrl = fileDemoUrl;
            _fileNavAUrl = fileNavAUrl;
            _fileNavBUrl = fileNavBUrl;
            _testOutputDir = testOutputDir;
            _runTest = runTest;
        }

        public void RunAll()
        {
            Directory.CreateDirectory(_testOutputDir);

            Console.WriteLine();
            Console.WriteLine("=== [OpenRPA] bridge-full-regression.xaml 镜像（file://）===");
            Console.WriteLine("Demo FILE : " + _fileDemoUrl);
            Console.WriteLine("Output    : " + _testOutputDir);
            Console.WriteLine();

            Section0BrowserOpen();
            Section1BrowserTab();
            Section2TopElements();
            Section3FindExistsParallel();
            Section4IframeLogin();
            Section5NestedIframe();
            Section6TabApi();
            Section7Navigate();
            Section8SwitchTab();
            Section9RefreshNewTabClose();
            Section10ClickForNewTabDownload();
            Section11BrowserOpenClose();
        }

        private void Section0BrowserOpen()
        {
            Console.WriteLine("--- [0] BrowserOpen ---");
            _runTest("[0] BrowserOpen demo home", () =>
            {
                _browser.BrowserOpen(out _demoTab, _fileDemoUrl);
                WaitForTitle(_demoTab, "GWIS SYSTEM");
                Thread.Sleep(500);
            });
        }

        private void Section1BrowserTab()
        {
            Console.WriteLine("--- [1] Browser / Tab ---");
            _runTest("[1] GetAllTabs", () =>
            {
                var tabs = _browser.GetAllTabs();
                AssertTrue(tabs.Length >= 1, "至少 1 个 Tab");
                AssertTrue(tabs.Any(t => t.TabId == _demoTab.TabId), "应含 demoTab");
            });

            _runTest("[1] GetActivatedTab", () =>
            {
                var tab = _browser.GetActivatedTab();
                AssertTrue(tab.TabId > 0, "激活 Tab 有效");
            });

            _runTest("[1] GetLatestTab", () =>
            {
                var tab = _browser.GetLatestTab();
                AssertTrue(tab.TabId > 0, "最新 Tab 有效");
            });

            _runTest("[1] GetTabInfo", () =>
            {
                var info = _demoTab.GetInfo();
                AssertFalse(info.IsClosed, "Tab 未关闭");
                AssertContains(info.Url, "index.html", "URL");
            });

            _runTest("[1] AttachBrowser (GetActivatedTab 等价)", () =>
            {
                var attached = _browser.GetActivatedTab();
                AssertTrue(attached.TabId > 0, "Attach 后 Tab 有效");
            });
        }

        private void Section2TopElements()
        {
            Console.WriteLine("--- [2] 顶层元素 ---");

            _runTest("[2] Click topBtn + GetText topStatus", () =>
            {
                _host.ClickElement(DemoSelectors.WithWnd(DemoSelectors.TopBtn), _demoTab);
                WaitForElementText(DemoSelectors.WithWnd(DemoSelectors.TopStatus), _demoTab, "clicked");
            });

            _runTest("[2] DoubleClick dblClickBtn", () =>
            {
                _host.FindElement(DemoSelectors.WithWnd(DemoSelectors.DblClickBtn), _demoTab).DoubleClick();
                WaitForElementText(DemoSelectors.WithWnd(DemoSelectors.DblClickStatus), _demoTab, "double-clicked");
            });

            _runTest("[2] Check / IsChecked / Uncheck agreeTerms", () =>
            {
                var el = _host.FindElement(DemoSelectors.WithWnd(DemoSelectors.AgreeTerms), _demoTab);
                el.Check();
                AssertTrue(el.IsChecked(), "应已勾选");
                el.Uncheck();
                AssertFalse(el.IsChecked(), "应已取消");
            });

            _runTest("[2] Select topCountry ValType=Value", () =>
            {
                var el = _host.FindElement(DemoSelectors.WithWnd(DemoSelectors.TopCountry), _demoTab);
                el.Select(BridgeSelectValueType.Value, values: new[] { "HK" }, validateContentAfterSelected: true);
                AssertTrue(el.GetSelected().Contains("HK"), "Value=HK");
            });

            _runTest("[2] Select topCountry ValType=Text", () =>
            {
                var el = _host.FindElement(DemoSelectors.WithWnd(DemoSelectors.TopCountry), _demoTab);
                el.Select(BridgeSelectValueType.Text, texts: new[] { "China" });
                AssertTrue(el.GetSelected().Contains("CN"), "Text=China -> CN");
            });

            _runTest("[2] Select topCountry ValType=Index", () =>
            {
                var el = _host.FindElement(DemoSelectors.WithWnd(DemoSelectors.TopCountry), _demoTab);
                el.Select(BridgeSelectValueType.Index, indices: new[] { 2 });
                var selected = el.GetSelected();
                AssertTrue(selected != null && selected.Length > 0, "Index=2 应有选中项");
            });

            _runTest("[2] GetSelected topCountry", () =>
            {
                var el = _host.FindElement(DemoSelectors.WithWnd(DemoSelectors.TopCountry), _demoTab);
                var selected = el.GetSelected();
                AssertTrue(selected != null && selected.Length > 0, "GetSelected 非空");
            });

            _runTest("[2] FindElement planPro + Click Element", () =>
            {
                var planPro = _host.FindElement(DemoSelectors.WithWnd(DemoSelectors.PlanPro), _demoTab);
                planPro.Click();
                AssertTrue(planPro.IsChecked(), "planPro 应选中");
            });

            _runTest("[2] Input topNotes + GetInputValue", () =>
            {
                var sel = DemoSelectors.WithWnd(DemoSelectors.TopNotes);
                _host.InputElement(sel, "line1\r\nline2", _demoTab);
                AssertContains(_host.GetInputValue(sel, _demoTab), "line1", "topNotes");
            });

            _runTest("[2] SendKeys Selector + Element (abc+def)", () =>
            {
                var sel = DemoSelectors.WithWnd(DemoSelectors.SendKeysTarget);
                _host.InputElement(sel, string.Empty, _demoTab);
                _host.ClickElement(sel, _demoTab);
                var el = _host.FindElement(sel, _demoTab);
                el.SendKeys("abc");
                el.SendKeys("def");
                AssertContains(el.GetInputValue(), "abcdef", "SendKeys 组合");
            });

            _runTest("[2] GetAttribute / SetAttribute setAttrBtn", () =>
            {
                var el = _host.FindElement(DemoSelectors.WithWnd(DemoSelectors.SetAttrBtn), _demoTab);
                AssertEqual("before", el.GetAttribute("data-test"), "初始");
                el.SetAttribute("data-test", "after");
                AssertEqual("after", el.GetAttribute("data-test"), "更新后");
            });

            _runTest("[2] GetRect rectBox", () =>
            {
                var rect = _host.FindElement(DemoSelectors.WithWnd(DemoSelectors.RectBox), _demoTab).GetRect();
                AssertTrue(rect.Width > 0 && rect.Height > 0, "rect 有效");
            });

            _runTest("[2] GetChildren + GetText + GetParent childTarget", () =>
            {
                var parent = _host.FindElement(DemoSelectors.WithWnd(DemoSelectors.ParentBox), _demoTab);
                var children = parent.GetChildren(DemoSelectors.ChildScoped);
                AssertTrue(children.Length >= 1, "GetChildren");
                var child = _host.FindElement(DemoSelectors.WithWnd(DemoSelectors.ChildTarget), _demoTab);
                AssertEqual("Child Text", child.GetText().Trim(), "child text");
                var parentOut = child.GetParent(1);
                AssertTrue(parentOut != null, "GetParent");
            });
        }

        private void Section3FindExistsParallel()
        {
            Console.WriteLine("--- [3] Find / Exists / ParallelFind ---");

            _runTest("[3] ElementExists topBtn / notExists", () =>
            {
                AssertTrue(_host.ElementExists(DemoSelectors.WithWnd(DemoSelectors.TopBtn), _demoTab), "topBtn 存在");
                AssertFalse(_host.ElementExists(DemoSelectors.WithWnd(DemoSelectors.NotExists), _demoTab), "notExists");
            });

            _runTest("[3] FindElement Tab-scoped topBtn", () =>
            {
                var el = _host.FindElement(DemoSelectors.TopBtn, _demoTab);
                AssertEqual("Top Action", el.GetText().Trim(), "Tab-scoped topBtn");
            });

            _runTest("[3] FindElement BaseOn=Element scoped", () =>
            {
                var parent = _host.FindElement(DemoSelectors.WithWnd(DemoSelectors.ParentBox), _demoTab);
                var child = parent.FindElement(DemoSelectors.ChildScoped);
                AssertEqual("Child Text", child.GetText().Trim(), "scoped child");
            });

            _runTest("[3] FindElements Tab / Element / empty", () =>
            {
                AssertTrue(_host.FindElements(DemoSelectors.WithWnd(DemoSelectors.TopBtn), _demoTab).Length == 1, "Tab FindElements");
                var parent = _host.FindElement(DemoSelectors.WithWnd(DemoSelectors.ParentBox), _demoTab);
                AssertTrue(parent.FindElements(DemoSelectors.ChildScoped).Length == 1, "Element FindElements");
                AssertTrue(_host.FindElements(DemoSelectors.WithWnd(DemoSelectors.NotExists), _demoTab).Length == 0, "empty");
            });

            _runTest("[3] ParallelFindElement Tab / Element", () =>
            {
                var idxTab = _demoTab.ParallelFindElement(new[] { DemoSelectors.BtnCandidateA, DemoSelectors.BtnCandidateB });
                AssertTrue(idxTab >= 0, "ParallelFind Tab");
                var parent = _host.FindElement(DemoSelectors.WithWnd(DemoSelectors.ParentBox), _demoTab);
                var idxEl = parent.ParallelFindElement(new[] { DemoSelectors.ChildScoped, DemoSelectors.NotExists });
                AssertTrue(idxEl == 0, "ParallelFind Element");
            });
        }

        private void Section4IframeLogin()
        {
            Console.WriteLine("--- [4] iframe LoginWinMain ---");

            _runTest("[4] iframe Input userID (wnd+frm)", () =>
            {
                var sel = DemoSelectors.InLogin(DemoSelectors.UserId);
                _host.InputElement(sel, "alice001", _demoTab);
                AssertEqual("alice001", _host.GetInputValue(sel, _demoTab), "userID full");
            });

            _runTest("[4] iframe Input userID (Tab+frm scoped)", () =>
            {
                var sel = DemoSelectors.TabScope(DemoSelectors.FrmLogin, DemoSelectors.UserId);
                _host.InputElement(sel, "bob002", _demoTab);
                AssertEqual("bob002", _host.GetInputValue(sel, _demoTab), "userID scoped");
            });

            _runTest("[4] iframe Select / Check / Radio", () =>
            {
                var dept = _host.FindElement(DemoSelectors.InLogin(DemoSelectors.DeptSelect), _demoTab);
                dept.Select(BridgeSelectValueType.Value, values: new[] { "IT" });
                AssertTrue(dept.GetSelected().Contains("IT"), "dept IT");
                var remember = _host.FindElement(DemoSelectors.InLogin(DemoSelectors.RememberMe), _demoTab);
                remember.Check();
                AssertTrue(remember.IsChecked(), "rememberMe");
                var gender = _host.FindElement(DemoSelectors.InLogin(DemoSelectors.GenderM), _demoTab);
                gender.Click();
                AssertTrue(gender.IsChecked(), "genderM");
            });

            _runTest("[4] iframe Login Click + GetText statusLabel", () =>
            {
                _host.InputElement(DemoSelectors.InLogin(DemoSelectors.UserId), "charlie003", _demoTab);
                _host.ClickElement(DemoSelectors.InLogin(DemoSelectors.BtnLogin), _demoTab);
                WaitForElementText(DemoSelectors.InLogin(DemoSelectors.StatusLabel), _demoTab, "Logged in as: charlie003");
            });
        }

        private void Section5NestedIframe()
        {
            Console.WriteLine("--- [5] 双层 iframe ---");

            _runTest("[5] nested Input innerCode + GetInputValue", () =>
            {
                var sel = DemoSelectors.InNested(DemoSelectors.InnerCode);
                _host.InputElement(sel, "NEST-001", _demoTab);
                AssertEqual("NEST-001", _host.GetInputValue(sel, _demoTab), "innerCode");
            });
        }

        private void Section6TabApi()
        {
            Console.WriteLine("--- [6] Tab 级 API ---");

            _runTest("[6] RunJs Tab document.title", () =>
            {
                var result = _demoTab.RunJs("return document.title;");
                AssertContains(Convert.ToString(result), "GWIS SYSTEM", "title");
            });

            _runTest("[6] RunJs Element innerText", () =>
            {
                var el = _host.FindElement(DemoSelectors.WithWnd(DemoSelectors.TopBtn), _demoTab);
                var result = el.RunJs("return this.innerText;");
                AssertContains(Convert.ToString(result), "Top Action", "innerText");
            });

            _runTest("[6] GetCookies Tab", () =>
            {
                AssertTrue(_demoTab.GetCookies() != null, "Tab cookies");
            });

            _runTest("[6] GetCookies Browser", () =>
            {
                AssertTrue(_browser.GetCookies() != null, "Browser cookies 无 tab 参数");
            });

            _runTest("[6] GetStorage Tab Local / Session", () =>
            {
                AssertEqual("demoValue", _demoTab.GetLocalStorage()["demoKey"], "Tab local");
                AssertEqual("sessionValue", _demoTab.GetSessionStorage()["sessionKey"], "Tab session");
            });

            _runTest("[6] GetStorage Browser Local / Session", () =>
            {
                AssertEqual("demoValue", _browser.GetLocalStorage()["demoKey"], "Browser local 无 tab");
                AssertEqual("sessionValue", _browser.GetSessionStorage()["sessionKey"], "Browser session 无 tab");
            });

            _runTest("[6] TakeScreenshot Tab FullPage", () =>
            {
                var path = Path.Combine(_testOutputDir, "bridge-tab-screenshot.png");
                _demoTab.TakeScreenshot(path, fullPage: true);
                AssertTrue(File.Exists(path), "Tab 截图: " + path);
            });

            _runTest("[6] TakeScreenshot Element rectBox", () =>
            {
                var path = Path.Combine(_testOutputDir, "bridge-element-screenshot.png");
                var rectBox = _host.FindElement(DemoSelectors.WithWnd(DemoSelectors.RectBox), _demoTab);
                rectBox.TakeScreenshot(path);
                AssertTrue(File.Exists(path), "Element 截图: " + path);
            });
        }

        private void Section7Navigate()
        {
            Console.WriteLine("--- [7] Navigate Back / Forward ---");

            _runTest("[7] NavigateUrl nav-a / nav-b / Back / Forward / home", () =>
            {
                _demoTab.NavigateUrl(_fileNavAUrl);
                WaitForTitle(_demoTab, "Nav A");
                Thread.Sleep(300);

                _demoTab.NavigateUrl(_fileNavBUrl);
                WaitForTitle(_demoTab, "Nav B");
                Thread.Sleep(300);

                _demoTab.Back();
                WaitForTitle(_demoTab, "Nav A");
                Thread.Sleep(300);

                _demoTab.Forward();
                WaitForTitle(_demoTab, "Nav B");
                Thread.Sleep(300);

                _demoTab.NavigateUrl(_fileDemoUrl);
                WaitForTitle(_demoTab, "GWIS SYSTEM");
                Thread.Sleep(300);
            });
        }

        private void Section8SwitchTab()
        {
            Console.WriteLine("--- [8] Browser SwitchTab ---");

            BwTab navTabB = null;
            _runTest("[8] NewTab nav-b for switch", () =>
            {
                navTabB = _browser.NewTab(_fileNavBUrl);
                WaitForTitle(navTabB, "Nav B");
                Thread.Sleep(300);
            });

            _runTest("[8] SwitchTab ByType=Title+UrlRe", () =>
            {
                var switched = _browser.SwitchTab(title: "GWIS Nav B", urlRe: "nav-b\\.html");
                AssertTrue(switched.TabId == navTabB.TabId, "Title+UrlRe");
            });

            _runTest("[8] SwitchTab ByType=TitleRegex home", () =>
            {
                var switched = _browser.SwitchTab(titleRe: "GWIS SYSTEM.*");
                AssertContains(switched.GetInfo().Title, "GWIS SYSTEM", "TitleRegex home");
                _demoTab = switched;
            });

            _runTest("[8] SwitchTab ByType=Url nav-a", () =>
            {
                var navA = _browser.NewTab(_fileNavAUrl);
                WaitForTitle(navA, "Nav A");
                var switched = _browser.SwitchTab(url: _fileNavAUrl);
                AssertContains(switched.GetInfo().Url ?? string.Empty, "nav-a.html", "Url nav-a");
            });

            _runTest("[8] SwitchTab ByType=UrlRegex nav-b", () =>
            {
                var switched = _browser.SwitchTab(urlRe: "nav-b\\.html");
                AssertContains(switched.GetInfo().Url ?? string.Empty, "nav-b.html", "UrlRegex nav-b");
            });

            _runTest("[8] SwitchTab ByType=Tab object", () =>
            {
                var switched = _browser.SwitchTab(tab: _demoTab);
                AssertTrue(switched.TabId == _demoTab.TabId, "Tab object");
            });

            _runTest("[8] SwitchTab ByType=Index 0", () =>
            {
                var switched = _browser.SwitchTab(0);
                AssertTrue(switched.TabId > 0, "Index 0");
            });
        }

        private void Section9RefreshNewTabClose()
        {
            Console.WriteLine("--- [9] Refresh / NewTab / Close ---");

            _runTest("[9] Refresh demo tab", () =>
            {
                _browser.SwitchTab(titleRe: "GWIS SYSTEM.*");
                _demoTab = _browser.GetActivatedTab();
                _demoTab.Refresh();
                WaitForTitle(_demoTab, "GWIS SYSTEM");
                Thread.Sleep(500);
            });

            _runTest("[9] NewTab nav-a + disposable nav-b + Close", () =>
            {
                var navA = _browser.NewTab(_fileNavAUrl);
                WaitForTitle(navA, "Nav A");
                var disposable = _browser.NewTab(_fileNavBUrl);
                WaitForTitle(disposable, "Nav B");
                var tabId = disposable.TabId;
                disposable.Close();
                WaitUntil(() => !_browser.GetAllTabs().Any(t => t.TabId == tabId), "Tab 应已关闭");
            });
        }

        private void Section10ClickForNewTabDownload()
        {
            Console.WriteLine("--- [10] ClickForNewTab / ClickForDownload ---");

            _runTest("[10] Switch home + ClickForNewTab + Close", () =>
            {
                _demoTab = _browser.SwitchTab(titleRe: "GWIS SYSTEM.*");
                Thread.Sleep(300);
                var el = _demoTab.FindElement(DemoSelectors.OpenNewTabLink);
                var newTab = el.ClickForNewTab();
                WaitForTitle(newTab, "Nav A");
                newTab.Close();
            });

            _runTest("[10] ClickForDownload downloadLink", () =>
            {
                _demoTab = _browser.SwitchTab(titleRe: "GWIS SYSTEM.*");
                var path = Path.Combine(_testOutputDir, "f2b-openrpa-download.txt");
                var el = _demoTab.FindElement(DemoSelectors.DownloadLink);
                var info = el.ClickForDownload(path);
                AssertTrue(File.Exists(path), "下载文件: " + path);
                AssertContains(File.ReadAllText(path), "F2B Bridge demo download", "下载内容");
                AssertEqual(path, info.SavedPath, "SavedPath");
            });
        }

        private void Section11BrowserOpenClose()
        {
            Console.WriteLine("--- [11] BrowserOpen second + BrowserClose ---");

            _runTest("[11] BrowserOpen second window + BrowserClose", () =>
            {
                BwTab secondTab;
                _browser.BrowserOpen(out secondTab, _fileDemoUrl);
                WaitForTitle(secondTab, "GWIS SYSTEM");
                Thread.Sleep(500);
                _browser.BrowserClose();
            });
        }

        private void WaitForTitle(BwTab tab, string titleContains, int timeoutMs = 10000)
        {
            WaitUntil(() =>
            {
                var title = tab.GetInfo().Title ?? string.Empty;
                return title.IndexOf(titleContains, StringComparison.OrdinalIgnoreCase) >= 0;
            }, "等待标题 '" + titleContains + "' 超时", timeoutMs);
        }

        private void WaitForElementText(string selector, BwTab tab, string expectedText, int timeoutMs = 5000)
        {
            WaitUntil(() =>
            {
                var text = _host.GetElementText(selector, tab) ?? string.Empty;
                return string.Equals(text.Trim(), expectedText, StringComparison.Ordinal);
            }, "等待元素文本 '" + expectedText + "' 超时", timeoutMs);
        }

        private static void WaitUntil(Func<bool> condition, string timeoutMessage, int timeoutMs = 10000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (condition())
                    return;
                Thread.Sleep(50);
            }

            throw new InvalidOperationException(timeoutMessage);
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void AssertFalse(bool condition, string message)
        {
            if (condition)
                throw new InvalidOperationException(message);
        }

        private static void AssertEqual(string expected, string actual, string label)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
                throw new InvalidOperationException(label + ": 期望 '" + expected + "'，实际 '" + actual + "'");
        }

        private static void AssertContains(string actual, string expected, string label)
        {
            if (actual == null || actual.IndexOf(expected, StringComparison.OrdinalIgnoreCase) < 0)
                throw new InvalidOperationException(label + ": '" + actual + "' 应包含 '" + expected + "'");
        }
    }
}
