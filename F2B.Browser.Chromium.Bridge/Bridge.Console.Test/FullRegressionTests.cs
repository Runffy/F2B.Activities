using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using F2B.Browser.Chromium.Bridge;

namespace F2B.Bridge.ConsoleTest
{
    internal sealed class FullRegressionTests
    {
        private readonly BridgeHost _host;
        private readonly BridgeSyncClient _client;
        private readonly BwBrowser _browser;
        private readonly string _baseUrl;
        private readonly string _fileDemoUrl;
        private BwTab _demoTab;

        private int _passed;
        private int _failed;
        private int _skipped;
        private readonly List<TestTiming> _timings = new List<TestTiming>();

        private sealed class TestTiming
        {
            public string Name { get; set; }
            public long ElapsedMs { get; set; }
            public bool Passed { get; set; }
        }

        public FullRegressionTests(BridgeHost host, BridgeSyncClient client, string baseUrl, string fileDemoUrl, string testOutputDir)
        {
            _host = host;
            _client = client;
            _browser = client.GetBrowser();
            _baseUrl = baseUrl;
            _fileDemoUrl = fileDemoUrl;
            _testOutputDir = testOutputDir;
        }

        private readonly string _testOutputDir;

        public int RunAll(bool runOpenRpaFull = true, bool runHttpRegression = false)
        {
            Console.WriteLine("=== 清理旧 Demo Tab ===");
            CleanupStaleDemoTabs();

            if (runOpenRpaFull)
                RunOpenRpaFullRegression();

            if (runHttpRegression)
                RunHttpFullRegression();

            PrintTimingSummary();
            Console.WriteLine("全量测试完成: 通过 " + _passed + " / 失败 " + _failed + " / 跳过 " + _skipped);
            return _failed == 0 ? 0 : 2;
        }

        private void RunOpenRpaFullRegression()
        {
            var demoRoot = Path.GetDirectoryName(new Uri(_fileDemoUrl).LocalPath);
            var fileNavA = new Uri(Path.Combine(demoRoot ?? string.Empty, "nav-a.html")).AbsoluteUri;
            var fileNavB = new Uri(Path.Combine(demoRoot ?? string.Empty, "nav-b.html")).AbsoluteUri;

            var openRpa = new OpenRpaFullRegressionTests(
                _host,
                _browser,
                _fileDemoUrl,
                fileNavA,
                fileNavB,
                _testOutputDir,
                RunTest);

            openRpa.RunAll();
        }

        private void RunHttpFullRegression()
        {
            Console.WriteLine();
            Console.WriteLine("=== [B] HTTP 全量回归 ===");
            Console.WriteLine("Demo HTTP: " + _baseUrl);
            Console.WriteLine();

            Console.WriteLine("=== 打开 Demo 主页 ===");
            _demoTab = _browser.NewTab(_baseUrl);
            WaitForTitle(_demoTab, "GWIS SYSTEM");
            Thread.Sleep(500);

            Console.WriteLine();
            Console.WriteLine("--- Tab / Browser ---");
            TestTabResolveWnd();
            TestBrowserGetAllTabs();
            TestBrowserGetActivatedTab();
            TestBrowserGetLatestTab();
            TestTabGetInfo();

            Console.WriteLine();
            Console.WriteLine("--- 顶层元素 ---");
            TestClickGetText();
            TestDoubleClick();
            TestCheckUncheckIsChecked();
            TestSelectGetSelected();
            TestRadioClick();
            TestInputTextarea();
            TestSendKeysElement();
            TestGetAttributeSetAttribute();
            TestGetRect();
            TestGetParentGetChildren();
            TestFindElementExists();
            TestFindElements();
            TestParallelFindElement();

            Console.WriteLine();
            Console.WriteLine("--- 页顶操作后 iframe（无 RunJs 预滚动，对齐 OpenRPA §11）---");
            TestOpenRpaPreIframeBlock(_demoTab, "HTTP");
            TestIframeCoreBlock(_demoTab, "HTTP");

            Console.WriteLine();
            Console.WriteLine("--- iframe 扩展用例 (Select/Check/Radio) ---");
            TestIframeSelectCheckboxRadio();

            Console.WriteLine();
            Console.WriteLine("--- Tab 级 API ---");
            TestRunJs();
            TestGetCookies();
            TestGetStorage();
            TestTabTakeScreenshot();

            Console.WriteLine();
            Console.WriteLine("--- 导航 Back / Forward ---");
            TestNavigateBackForward();

            Console.WriteLine();
            Console.WriteLine("--- Browser SwitchTab ---");
            TestBrowserSwitchTab();

            Console.WriteLine();
            Console.WriteLine("--- Tab Refresh / NewTab / Close ---");
            TestTabRefresh();
            TestNewTab();
            TestTabClose();

            Console.WriteLine();
            Console.WriteLine("--- ClickForNewTab / ClickForDownload / BrowserOpen ---");
            TestClickForNewTab();
            TestClickForDownload();
            TestBrowserOpenClose();

        }

        private void TestOpenRpaPreIframeBlock(BwTab tab, string prefix)
        {
            RunTest(prefix + "/Click topBtn (wnd)", () =>
            {
                _host.ClickElement(DemoSelectors.WithWnd(DemoSelectors.TopBtn), tab);
                WaitForElementText(DemoSelectors.WithWnd(DemoSelectors.TopStatus), tab, "clicked");
            });

            RunTest(prefix + "/FindElement + FindElements + ParallelFind", () =>
            {
                _host.FindElement(DemoSelectors.WithWnd(DemoSelectors.TopBtn), tab);
                var elements = _host.FindElements(DemoSelectors.WithWnd(DemoSelectors.TopBtn), tab);
                AssertTrue(elements != null && elements.Length == 1, "FindElements topBtn count");
                var idx = tab.ParallelFindElement(new[] { DemoSelectors.BtnCandidateA, DemoSelectors.BtnCandidateB });
                AssertTrue(idx >= 0, "ParallelFindElement");
            });
        }

        private void TestIframeCoreBlock(BwTab tab, string prefix)
        {
            RunTest(prefix + "/iframe Input userID (wnd+frm, 无预滚动)", () =>
            {
                var sel = DemoSelectors.InLogin(DemoSelectors.UserId);
                _host.InputElement(sel, "alice001", tab);
                AssertEqual("alice001", _host.GetInputValue(sel, tab), "userID full wnd+frm");
            });

            RunTest(prefix + "/iframe Input userID (Tab+frm scoped)", () =>
            {
                var sel = DemoSelectors.TabScope(DemoSelectors.FrmLogin, DemoSelectors.UserId);
                _host.InputElement(sel, "bob002", tab);
                AssertEqual("bob002", _host.GetInputValue(sel, tab), "userID scoped");
            });

            RunTest(prefix + "/iframe Login flow", () =>
            {
                _host.InputElement(DemoSelectors.InLogin(DemoSelectors.UserId), "charlie003", tab);
                _host.ClickElement(DemoSelectors.InLogin(DemoSelectors.BtnLogin), tab);
                WaitForElementText(DemoSelectors.InLogin(DemoSelectors.StatusLabel), tab, "Logged in as: charlie003");
            });

            RunTest(prefix + "/nested iframe innerCode", () =>
            {
                var sel = DemoSelectors.InNested(DemoSelectors.InnerCode);
                _host.InputElement(sel, "NEST-001", tab);
                AssertEqual("NEST-001", _host.GetInputValue(sel, tab), "innerCode");
            });
        }

        private void TestTabResolveWnd()
        {
            RunTest("TabResolve wnd 匹配 Demo", () =>
            {
                var ctx = _host.ResolveContext(DemoSelectors.WndMain);
                AssertTrue(ctx.Tab.TabId == _demoTab.TabId, "TabId 应匹配");
                AssertContains(ctx.Tab.Title, "GWIS SYSTEM", "Tab 标题");
            });
        }

        private void TestBrowserGetAllTabs()
        {
            RunTest("BrowserGetAllTab", () =>
            {
                var tabs = _browser.GetAllTabs();
                AssertTrue(tabs.Length >= 1, "至少 1 个 Tab");
                AssertTrue(tabs.Any(t => t.TabId == _demoTab.TabId), "应包含 Demo Tab");
            });
        }

        private void TestBrowserGetActivatedTab()
        {
            RunTest("BrowserGetActivatedTab", () =>
            {
                var tab = _browser.GetActivatedTab();
                AssertTrue(tab.TabId > 0, "激活 Tab 有效");
            });
        }

        private void TestBrowserGetLatestTab()
        {
            RunTest("BrowserGetLatestTab", () =>
            {
                var tab = _browser.GetLatestTab();
                AssertTrue(tab.TabId > 0, "最新 Tab 有效");
            });
        }

        private void TestTabGetInfo()
        {
            RunTest("TabGetInfo", () =>
            {
                var info = _demoTab.GetInfo();
                AssertFalse(info.IsClosed, "Tab 未关闭");
                AssertContains(info.Url, "127.0.0.1", "URL");
            });
        }

        private void TestClickGetText()
        {
            RunTest("Click + GetText (topBtn)", () =>
            {
                _host.ClickElement(DemoSelectors.WithWnd(DemoSelectors.TopBtn), _demoTab);
                WaitForElementText(DemoSelectors.WithWnd(DemoSelectors.TopStatus), _demoTab, "clicked");
            });
        }

        private void TestDoubleClick()
        {
            RunTest("DoubleClick", () =>
            {
                var el = _host.FindElement(DemoSelectors.WithWnd(DemoSelectors.DblClickBtn), _demoTab);
                el.DoubleClick();
                WaitForElementText(DemoSelectors.WithWnd(DemoSelectors.DblClickStatus), _demoTab, "double-clicked");
            });
        }

        private void TestCheckUncheckIsChecked()
        {
            RunTest("Check / Uncheck / IsChecked (agreeTerms)", () =>
            {
                var sel = DemoSelectors.WithWnd(DemoSelectors.AgreeTerms);
                var el = _host.FindElement(sel, _demoTab);
                el.Check();
                AssertTrue(el.IsChecked(), "应已勾选");
                el.Uncheck();
                AssertFalse(el.IsChecked(), "应已取消勾选");
            });
        }

        private void TestSelectGetSelected()
        {
            RunTest("Select + GetSelected (topCountry)", () =>
            {
                var sel = DemoSelectors.WithWnd(DemoSelectors.TopCountry);
                var el = _host.FindElement(sel, _demoTab);
                el.Select(BridgeSelectValueType.Value, values: new[] { "HK" });
                var selected = el.GetSelected();
                AssertTrue(selected.Contains("HK"), "GetSelected 应含 HK");
            });
        }

        private void TestRadioClick()
        {
            RunTest("Radio Click (planPro)", () =>
            {
                var el = _host.FindElement(DemoSelectors.WithWnd(DemoSelectors.PlanPro), _demoTab);
                el.Click();
                AssertTrue(el.IsChecked(), "radio 应选中");
            });
        }

        private void TestInputTextarea()
        {
            RunTest("Input + GetInputValue (topNotes)", () =>
            {
                var sel = DemoSelectors.WithWnd(DemoSelectors.TopNotes);
                _host.InputElement(sel, "line1\r\nline2", _demoTab);
                var value = _host.GetInputValue(sel, _demoTab);
                AssertContains(value, "line1", "textarea 内容");
            });
        }

        private void TestSendKeysElement()
        {
            RunTest("SendKeys (element)", () =>
            {
                var el = _host.FindElement(DemoSelectors.WithWnd(DemoSelectors.SendKeysTarget), _demoTab);
                el.Input(string.Empty);
                el.Click();
                el.SendKeys("abc");
                var value = el.GetInputValue();
                AssertContains(value, "abc", "SendKeys 结果");
            });
        }

        private void TestGetAttributeSetAttribute()
        {
            RunTest("GetAttribute + SetAttribute (setAttrBtn)", () =>
            {
                var sel = DemoSelectors.WithWnd(DemoSelectors.SetAttrBtn);
                var el = _host.FindElement(sel, _demoTab);
                AssertEqual("before", el.GetAttribute("data-test"), "初始 data-test");
                el.SetAttribute("data-test", "after");
                AssertEqual("after", el.GetAttribute("data-test"), "更新后 data-test");
            });
        }

        private void TestGetRect()
        {
            RunTest("GetRect (rectBox)", () =>
            {
                var el = _host.FindElement(DemoSelectors.WithWnd(DemoSelectors.RectBox), _demoTab);
                var rect = el.GetRect();
                AssertTrue(rect.Width > 0 && rect.Height > 0, "矩形宽高应 > 0");
            });
        }

        private void TestGetParentGetChildren()
        {
            RunTest("GetParent + GetChildren (parentBox)", () =>
            {
                var parent = _host.FindElement(DemoSelectors.WithWnd(DemoSelectors.ParentBox), _demoTab);
                var children = parent.GetChildren(DemoSelectors.ChildScoped);
                AssertTrue(children.Length >= 1, "GetChildren 应返回 childTarget");

                var child = _host.FindElement(DemoSelectors.WithWnd(DemoSelectors.ChildTarget), _demoTab);
                AssertEqual("Child Text", child.GetText().Trim(), "child text");
                child.GetParent();
            });
        }

        private void TestFindElementExists()
        {
            RunTest("FindElement + ElementExists", () =>
            {
                AssertTrue(_host.ElementExists(DemoSelectors.WithWnd(DemoSelectors.TopBtn), _demoTab), "topBtn 存在");
                AssertFalse(_host.ElementExists(DemoSelectors.WithWnd(DemoSelectors.NotExists), _demoTab), "不存在元素");
                _demoTab.FindElement(DemoSelectors.TopBtn);
            });
        }

        private void TestFindElements()
        {
            RunTest("FindElements (Tab, instant)", () =>
            {
                var elements = _host.FindElements(DemoSelectors.WithWnd(DemoSelectors.TopBtn), _demoTab);
                AssertTrue(elements.Length == 1, "topBtn 当前应匹配 1 个，实际=" + elements.Length);
                AssertEqual("Top Action", elements[0].GetText().Trim(), "topBtn text");
            });

            RunTest("FindElements (Element, instant)", () =>
            {
                var parent = _host.FindElement(DemoSelectors.WithWnd(DemoSelectors.ParentBox), _demoTab);
                var elements = parent.FindElements(DemoSelectors.ChildScoped);
                AssertTrue(elements.Length == 1, "parentBox 下 childScoped 应匹配 1 个，实际=" + elements.Length);
                AssertEqual("Child Text", elements[0].GetText().Trim(), "child text");
            });

            RunTest("FindElements empty snapshot", () =>
            {
                var elements = _host.FindElements(DemoSelectors.WithWnd(DemoSelectors.NotExists), _demoTab);
                AssertTrue(elements.Length == 0, "不存在元素应返回空数组");
            });
        }

        private void TestParallelFindElement()
        {
            RunTest("ParallelFindElement (Tab)", () =>
            {
                var idx = _demoTab.ParallelFindElement(new[]
                {
                    DemoSelectors.BtnCandidateA,
                    DemoSelectors.BtnCandidateB
                });
                AssertTrue(idx >= 0 && idx <= 1, "matchedIndex 应为 0 或 1，实际=" + idx);
            });

            RunTest("ParallelFindElement (Element)", () =>
            {
                var parent = _host.FindElement(DemoSelectors.WithWnd(DemoSelectors.ParentBox), _demoTab);
                var idx = parent.ParallelFindElement(new[]
                {
                    DemoSelectors.ChildScoped,
                    DemoSelectors.NotExists
                });
                AssertTrue(idx == 0, "parentBox 下应先匹配 childScoped，实际=" + idx);
            });
        }

        private void TestIframeSelectCheckboxRadio()
        {
            RunTest("iframe Select / Check / Radio", () =>
            {
                var dept = _host.FindElement(DemoSelectors.InLogin(DemoSelectors.DeptSelect));
                dept.Select(BridgeSelectValueType.Value, values: new[] { "IT" });
                AssertTrue(dept.GetSelected().Contains("IT"), "dept IT");

                var remember = _host.FindElement(DemoSelectors.InLogin(DemoSelectors.RememberMe));
                remember.Check();
                AssertTrue(remember.IsChecked(), "rememberMe");

                var gender = _host.FindElement(DemoSelectors.InLogin(DemoSelectors.GenderM));
                gender.Click();
                AssertTrue(gender.IsChecked(), "genderM");
            });
        }

        private void TestRunJs()
        {
            RunTest("RunJs (document.title)", () =>
            {
                var result = _demoTab.RunJs("return document.title;");
                AssertContains(Convert.ToString(result), "GWIS SYSTEM", "document.title");
            });

            RunTest("RunJs (element innerText)", () =>
            {
                var el = _host.FindElement(DemoSelectors.WithWnd(DemoSelectors.TopBtn), _demoTab);
                var result = el.RunJs("return this.innerText;");
                AssertContains(Convert.ToString(result), "Top Action", "innerText");
            });
        }

        private void TestGetCookies()
        {
            RunTest("GetCookies (Tab)", () =>
            {
                var cookies = _demoTab.GetCookies();
                AssertTrue(cookies != null, "cookies 非 null");
            });

            RunTest("GetCookies (Browser)", () =>
            {
                var cookies = _browser.GetCookies(_demoTab);
                AssertTrue(cookies != null, "browser cookies 非 null");
            });
        }

        private void TestGetStorage()
        {
            RunTest("GetStorage Tab local + session", () =>
            {
                var local = _demoTab.GetLocalStorage();
                AssertEqual("demoValue", local.ContainsKey("demoKey") ? local["demoKey"] : null, "localStorage demoKey");
                var session = _demoTab.GetSessionStorage();
                AssertEqual("sessionValue", session.ContainsKey("sessionKey") ? session["sessionKey"] : null, "sessionStorage sessionKey");
            });

            RunTest("GetStorage Browser local + session", () =>
            {
                var local = _browser.GetLocalStorage(_demoTab);
                AssertEqual("demoValue", local.ContainsKey("demoKey") ? local["demoKey"] : null, "browser localStorage demoKey");
                var session = _browser.GetSessionStorage(_demoTab);
                AssertEqual("sessionValue", session.ContainsKey("sessionKey") ? session["sessionKey"] : null, "browser sessionStorage sessionKey");
            });
        }

        private void TestTabTakeScreenshot()
        {
            RunTest("TakeScreenshot (tab)", () =>
            {
                var path = Path.Combine(Path.GetTempPath(), "f2b-bridge-tab-screenshot.png");
                _demoTab.TakeScreenshot(path);
                AssertTrue(File.Exists(path), "截图文件应存在: " + path);
                try { File.Delete(path); } catch { /* ignore */ }
            });
        }

        private void TestNavigateBackForward()
        {
            RunTest("Navigate + Back + Forward", () =>
            {
                _demoTab.NavigateUrl(_baseUrl + "nav-a.html");
                WaitForTitle(_demoTab, "Nav A");

                _demoTab.NavigateUrl(_baseUrl + "nav-b.html");
                WaitForTitle(_demoTab, "Nav B");

                _demoTab.Back();
                WaitForTitle(_demoTab, "Nav A");

                _demoTab.Forward();
                WaitForTitle(_demoTab, "Nav B");

                _demoTab.NavigateUrl(_baseUrl);
                WaitForTitle(_demoTab, "GWIS SYSTEM");
            });
        }

        private void TestBrowserSwitchTab()
        {
            RunTest("BrowserSwitchTab (by title + urlRe)", () =>
            {
                var navTab = _browser.NewTab(_baseUrl + "nav-b.html");
                WaitForTitle(navTab, "Nav B");
                var switched = _browser.SwitchTab(title: "GWIS Nav B", urlRe: "nav-b\\.html");
                AssertTrue(switched.TabId == navTab.TabId, "SwitchTab 应切到最新 Nav B");
                AssertContains(switched.GetInfo().Title, "Nav B", "Nav B 标题");
                _browser.SwitchTab(titleRe: "GWIS SYSTEM.*");
            });
        }

        private void TestTabRefresh()
        {
            RunTest("TabRefresh", () =>
            {
                _demoTab.Refresh();
                WaitForUrl(_demoTab, "127.0.0.1");
            });
        }

        private void TestNewTab()
        {
            RunTest("NewTab", () =>
            {
                var tab = _browser.NewTab(_baseUrl + "nav-a.html");
                WaitForTitle(tab, "Nav A");
            });
        }

        private void TestTabClose()
        {
            RunTest("TabClose", () =>
            {
                var disposable = _browser.NewTab(_baseUrl + "nav-b.html");
                WaitForTitle(disposable, "Nav B");
                var tabId = disposable.TabId;
                disposable.Close();
                WaitUntil(() => !_browser.GetAllTabs().Any(t => t.TabId == tabId), "Tab 应已关闭");
            });
        }

        private void TestClickForNewTab()
        {
            RunTest("ClickForNewTab", () =>
            {
                var el = _demoTab.FindElement(DemoSelectors.OpenNewTabLink);
                var newTab = el.ClickForNewTab();
                WaitForTitle(newTab, "Nav A");
                newTab.Close();
            });
        }

        private void TestClickForDownload()
        {
            RunTest("ClickForDownload", () =>
            {
                var path = Path.Combine(Path.GetTempPath(), "f2b-download-" + Guid.NewGuid().ToString("N") + ".txt");
                var el = _demoTab.FindElement(DemoSelectors.DownloadLink);
                var info = el.ClickForDownload(path);
                AssertTrue(File.Exists(path), "下载文件应存在: " + path);
                AssertContains(File.ReadAllText(path), "F2B Bridge demo download", "下载内容");
                AssertEqual(path, info.SavedPath, "SavedPath");
                try { File.Delete(path); } catch { /* ignore */ }
            });
        }

        private void TestBrowserOpenClose()
        {
            RunTest("BrowserOpen / BrowserClose", () =>
            {
                BwTab initialTab;
                _browser.BrowserOpen(out initialTab, _baseUrl);
                WaitForTitle(initialTab, "GWIS SYSTEM");
                AssertTrue(initialTab.TabId > 0, "initialTab 有效");
                _browser.BrowserClose();
            });
        }

        private void CleanupStaleDemoTabs()
        {
            var httpMarker = new Uri(_baseUrl).Authority;
            var fileMarker = "index.html";
            foreach (var tab in _browser.GetAllTabs())
            {
                var info = tab.GetInfo();
                var url = info.Url ?? string.Empty;
                var isDemoTab = url.IndexOf(httpMarker, StringComparison.OrdinalIgnoreCase) >= 0
                    || (url.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
                        && url.IndexOf(fileMarker, StringComparison.OrdinalIgnoreCase) >= 0);
                if (!isDemoTab)
                    continue;

                try
                {
                    tab.Close();
                    Thread.Sleep(100);
                }
                catch
                {
                    // ignore stale tabs
                }
            }
        }

        private void RunTest(string name, Action action)
        {
            Console.Write("[" + name + "] ... ");
            var sw = Stopwatch.StartNew();
            try
            {
                action();
                sw.Stop();
                _passed++;
                _timings.Add(new TestTiming { Name = name, ElapsedMs = sw.ElapsedMilliseconds, Passed = true });
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("OK  " + sw.ElapsedMilliseconds + " ms");
                Console.ResetColor();
                BridgeFileLog.Write("TEST PASS: " + name + " (" + sw.ElapsedMilliseconds + " ms)");
            }
            catch (Exception ex)
            {
                sw.Stop();
                _timings.Add(new TestTiming { Name = name, ElapsedMs = sw.ElapsedMilliseconds, Passed = false });
                _failed++;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("FAIL  " + sw.ElapsedMilliseconds + " ms");
                Console.WriteLine("  " + ex.Message);
                Console.ResetColor();
                BridgeFileLog.Write("TEST FAIL: " + name + " (" + sw.ElapsedMilliseconds + " ms) => " + ex.Message);
            }
        }

        private void PrintTimingSummary()
        {
            Console.WriteLine("=== 各功能耗时（指令发出 → 完成）===");
            const string header = "功能                                              耗时(ms)  结果";
            Console.WriteLine(header);
            Console.WriteLine(new string('-', header.Length));

            foreach (var timing in _timings)
            {
                var line = timing.Name.PadRight(48) + timing.ElapsedMs.ToString().PadLeft(9) + "  "
                    + (timing.Passed ? "OK" : "FAIL");
                Console.WriteLine(line);
                BridgeFileLog.Write("TEST TIMING: " + timing.Name + " => " + timing.ElapsedMs + " ms (" + (timing.Passed ? "OK" : "FAIL") + ")");
            }

            var passedTimings = _timings.Where(item => item.Passed).ToList();
            if (passedTimings.Count == 0)
                return;

            var totalMs = passedTimings.Sum(item => item.ElapsedMs);
            var slowest = passedTimings.OrderByDescending(item => item.ElapsedMs).First();
            var fastest = passedTimings.OrderBy(item => item.ElapsedMs).First();

            Console.WriteLine(new string('-', header.Length));
            Console.WriteLine("合计 " + totalMs + " ms | 平均 " + (totalMs / passedTimings.Count) + " ms");
            Console.WriteLine("最快: " + fastest.Name + " (" + fastest.ElapsedMs + " ms)");
            Console.WriteLine("最慢: " + slowest.Name + " (" + slowest.ElapsedMs + " ms)");
        }

        private static void WaitForTitle(BwTab tab, string titleContains, int timeoutMs = 10000)
        {
            WaitUntil(() =>
            {
                var title = tab.GetInfo().Title ?? string.Empty;
                return title.IndexOf(titleContains, StringComparison.OrdinalIgnoreCase) >= 0;
            }, "等待标题 '" + titleContains + "' 超时", timeoutMs);
        }

        private static void WaitForUrl(BwTab tab, string urlContains, int timeoutMs = 10000)
        {
            WaitUntil(() =>
            {
                var url = tab.GetInfo().Url ?? string.Empty;
                return url.IndexOf(urlContains, StringComparison.OrdinalIgnoreCase) >= 0;
            }, "等待 URL 含 '" + urlContains + "' 超时", timeoutMs);
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

        private void RunSkipped(string name, string reason)
        {
            _skipped++;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("[" + name + "] ... ");
            Console.WriteLine("SKIP");
            Console.WriteLine("  " + reason);
            Console.ResetColor();
            BridgeFileLog.Write("TEST SKIP: " + name + " => " + reason);
        }

        private void RunSkipped(string name, Action action)
        {
            Console.Write("[" + name + "] ... ");
            try
            {
                action();
                _failed++;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("FAIL (expected skip but passed)");
                Console.ResetColor();
                BridgeFileLog.Write("TEST FAIL: " + name + " => unexpectedly passed");
            }
            catch (Exception ex)
            {
                _skipped++;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("SKIP");
                Console.WriteLine("  " + ex.Message);
                Console.ResetColor();
                BridgeFileLog.Write("TEST SKIP: " + name + " => " + ex.Message);
            }
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
