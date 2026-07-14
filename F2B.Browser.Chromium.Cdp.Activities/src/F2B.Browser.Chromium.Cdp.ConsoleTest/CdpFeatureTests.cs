using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using F2B.Browser.Chromium.Cdp.Browser;
using F2B.Browser.Chromium.Cdp.Exceptions;

namespace F2B.Browser.Chromium.Cdp.ConsoleTest
{
    internal sealed class CdpFeatureTests
    {
        private readonly CdpBrowser _browser;
        private readonly bool _keepBrowser;
        private int _passed;
        private int _failed;

        internal CdpFeatureTests(CdpBrowser browser, bool keepBrowser)
        {
            _browser = browser;
            _keepBrowser = keepBrowser;
        }

        internal int RunAll()
        {
            CdpTab tab = null;
            try
            {
                var pageUrl = TestPageGenerator.CreateAndGetFileUri();
                tab = _browser.NewTab(pageUrl, background: false);
                _browser.ActivateTab(tab);
                tab.WaitForDocumentComplete(CdpDocumentWaitScope.MainDocument, 15000);

                SafeRun(() => RunNavigationTests(tab, pageUrl));
                SafeRun(() => RunTabTests(tab));
                SafeRun(() => RunTabFindTests(tab));
                SafeRun(() => RunElementPropertyTests(tab));
                SafeRun(() => RunElementActionTests(tab));
                SafeRun(() => RunSelectTests(tab));
                SafeRun(() => RunElementFindTests(tab));
                SafeRun(() => RunSendKeysTests(tab));
                SafeRun(() => RunScrollTests(tab));
                SafeRun(() => RunScreenshotTests(tab));
                SafeRun(() => RunParallelFindTests(tab));
                SafeRun(() => RunExtendedTests(tab));
                SafeRun(() => RunNestedDynamicFrameTests(tab, pageUrl));
                SafeRun(() => RunHttpApiTests(tab, pageUrl));
            }
            finally
            {
                if (tab != null)
                {
                    try
                    {
                        tab.Dispose();
                    }
                    catch
                    {
                    }
                }

                if (!_keepBrowser)
                {
                    try
                    {
                        _browser.Quit();
                    }
                    catch
                    {
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine("========== Summary ==========");
            Console.WriteLine("  Passed: {0}", _passed);
            Console.WriteLine("  Failed: {0}", _failed);
            Console.WriteLine("  Total:  {0}", _passed + _failed);

            return _failed == 0 ? 0 : 1;
        }

        private void SafeRun(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _failed++;
                Console.WriteLine("  [FAIL] Section error - {0}", ex.Message);
            }
        }

        private void RunTabTests(CdpTab tab)
        {
            Section("Tab properties & APIs");

            Assert("Tab.States.IsAlive", tab.States.IsAlive);
            Assert("Tab.States.ReadyState complete", tab.States.ReadyState == "complete");
            Assert("Tab.UserAgent not empty", !string.IsNullOrEmpty(tab.UserAgent));
            Assert("Tab.Html contains title", tab.Html.IndexOf("F2B CDP Test", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert("Tab.RunJs expression", Convert.ToInt32(tab.RunJs("1 + 2", asExpression: true)) == 3);
            Assert("Tab.RunJs function", Convert.ToString(tab.RunJs("document.querySelector('#page-title').innerText", asExpression: true)) == "F2B CDP Test");
            Assert("Tab.RunJs with args", Convert.ToString(tab.RunJs("return arguments[0] + arguments[1];", new object[] { "a", "b" })) == "ab");
            Assert("Tab.SessionStorage", tab.SessionStorage.ContainsKey("f2b-test") && tab.SessionStorage["f2b-test"] == "session-value");
            Assert("Tab.LocalStorage", tab.LocalStorage.ContainsKey("f2b-test") && tab.LocalStorage["f2b-test"] == "local-value");
            Assert("Tab.Rect.Size width > 0", tab.Rect.Size.Item1 > 0);

            Assert("Tab.Browser reference", ReferenceEquals(tab.Browser, _browser));

            tab.Browser.Maximize(tab);
            Thread.Sleep(200);
            Assert("Browser.Maximize", tab.Rect.WindowState == "maximized");
            tab.Browser.Normal(tab);
            Thread.Sleep(200);
            Assert("Browser.Normal", tab.Rect.WindowState == "normal");
            tab.Full();
            Thread.Sleep(200);
            Assert("Tab.Full", tab.Rect.WindowState == "fullscreen");
            tab.Browser.Normal(tab);
            Thread.Sleep(200);
            Pass("Browser window state APIs");

            tab.WaitForDocumentComplete(CdpDocumentWaitScope.MainDocument, 5000);
            Pass("WaitForDocumentComplete MainDocument");
        }

        private void RunNavigationTests(CdpTab tab, string pageUrl)
        {
            Section("Tab navigation");

            var originalMarker = "F2B CDP Test";
            var secondPageUrl = TestPageGenerator.CreateNavSecondPageUri();
            tab.WaitForDocumentComplete(CdpDocumentWaitScope.MainDocument, 15000);
            Assert("Navigation baseline title", tab.Title.IndexOf(originalMarker, StringComparison.OrdinalIgnoreCase) >= 0);

            tab.Navigate(secondPageUrl);
            tab.WaitForDocumentComplete(CdpDocumentWaitScope.MainDocument, 15000);
            Assert("Navigate to second page", tab.ElementExists("<ctrl id=\"nav-page-2\" />"));

            tab.Back();
            tab.WaitForDocumentComplete(CdpDocumentWaitScope.MainDocument, 15000);
            Assert("Back to test page", tab.FindElement("<ctrl id=\"page-title\" />", 5000, false) != null);

            tab.Forward();
            tab.WaitForDocumentComplete(CdpDocumentWaitScope.MainDocument, 15000);
            Assert("Forward to second page", tab.ElementExists("<ctrl id=\"nav-page-2\" />"));

            tab.Navigate(pageUrl);
            tab.WaitForDocumentComplete(CdpDocumentWaitScope.MainDocument, 15000);
            tab.Refresh();
            tab.WaitForDocumentComplete(CdpDocumentWaitScope.MainDocument, 15000);
            Assert("Refresh keeps test page", tab.Title.IndexOf(originalMarker, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void RunNestedDynamicFrameTests(CdpTab tab, string restorePageUrl)
        {
            Section("Nested dynamic iframes (delay / title / swap)");

            const string deepRelative =
                "<frm id=\"nf-l1\" />\r\n" +
                "<frm title=\"Nested Level 2\" />\r\n" +
                "<frm id=\"nf-l3\" />\r\n" +
                "<ctrl id=\"deep-input\" />";
            const string titleOnlyFrm = "<frm title=\"Nested Level 2\" />";
            const string titleReFrm = "<frm title-re=\"Nested Level.*\" />";
            const int nestTimeout = 20000;

            var listener = StartNestedFramesHttpServer();
            var baseUrl = listener.Prefixes.First().TrimEnd('/');
            try
            {
                tab.Navigate(baseUrl + "/nested-frames");
                // MainDocument can complete before dynamically created inner frames exist.
                tab.WaitForDocumentComplete(CdpDocumentWaitScope.MainDocument, 15000);
                tab.WaitForDocumentComplete(CdpDocumentWaitScope.AllDocuments, nestTimeout);

                var l1 = tab.FindFrame("<frm id=\"nf-l1\" />", nestTimeout);
                Assert("FindFrame nf-l1", l1 != null && !string.IsNullOrEmpty(l1.FrameId));

                var l2 = l1.FindFrame(titleOnlyFrm, nestTimeout);
                Assert("FindFrame by title (no id/name)",
                    l2 != null &&
                    !string.IsNullOrEmpty(l2.FrameId) &&
                    l2.Element != null &&
                    l2.Element.Tag == "iframe" &&
                    string.Equals(l2.Element.Attr("title"), "Nested Level 2", StringComparison.OrdinalIgnoreCase));

                var l2ByRe = l1.FindFrame(titleReFrm, nestTimeout);
                Assert("FindFrame by title-re",
                    l2ByRe != null && l2ByRe.FrameId == l2.FrameId);

                var l3 = l2.FindFrame("<frm id=\"nf-l3\" />", nestTimeout);
                Assert("FindFrame nf-l3 under title frame",
                    l3 != null && !string.IsNullOrEmpty(l3.FrameId));

                var deep = tab.FindElement(deepRelative, nestTimeout);
                Assert("Deep FindElement nested-ok",
                    deep != null && deep.Value == "nested-ok");

                deep.Input("nested-written", clear: true);
                Assert("Deep Input rewrite",
                    tab.FindElement(deepRelative, 5000).Value == "nested-written");
                deep.Input("nested-ok", clear: true);

                var bodyEl = tab.FindElement("<ctrl tag=\"body\" />", 0);
                var l1FromBody = bodyEl.FindFrame("<frm id=\"nf-l1\" />", nestTimeout);
                Assert("FindFrame nf-l1 from Element root",
                    l1FromBody != null && l1FromBody.FrameId == l1.FrameId);
                // Frame from Element FindFrame has empty FrameLevelsFromTab; relative FindFrame must still work.
                var l2FromL1 = l1FromBody.FindFrame(titleOnlyFrm, nestTimeout);
                Assert("FindFrame title under L1 Frame (from Element)",
                    l2FromL1 != null && l2FromL1.FrameId == l2.FrameId);

                var swapBtnSelector =
                    "<frm id=\"nf-l1\" />\r\n" +
                    "<frm title=\"Nested Level 2\" />\r\n" +
                    "<ctrl id=\"btn-swap-deep\" />";
                var swapBtn = tab.FindElement(swapBtnSelector, nestTimeout);
                Assert("Find swap button in L2", swapBtn != null);
                swapBtn.Click();

                var swappedSelector =
                    "<frm id=\"nf-l1\" />\r\n" +
                    "<frm title=\"Nested Level 2\" />\r\n" +
                    "<frm id=\"nf-l3\" />\r\n" +
                    "<ctrl id=\"deep-input-v2\" />";
                var swapped = tab.FindElement(swappedSelector, nestTimeout);
                Assert("After src swap deep-input-v2",
                    swapped != null && swapped.Value == "nested-swapped");
            }
            finally
            {
                try
                {
                    tab.Navigate(restorePageUrl);
                    tab.WaitForDocumentComplete(CdpDocumentWaitScope.MainDocument, 10000);
                }
                catch
                {
                }

                StopHttpListener(listener);
            }
        }

        private void RunHttpApiTests(CdpTab tab, string restorePageUrl)
        {
            Section("Tab Get/Post on local http page");

            var listener = StartLocalHttpServer();
            var baseUrl = listener.Prefixes.First().TrimEnd('/');
            try
            {
                tab.Navigate(baseUrl + "/");
                tab.WaitForDocumentComplete(CdpDocumentWaitScope.MainDocument, 10000);
                Assert("Tab navigates to local http", tab.Url.IndexOf("127.0.0.1", StringComparison.OrdinalIgnoreCase) >= 0);

                var getResponse = tab.Get(baseUrl + "/");
                Assert("Tab.Get local http", getResponse.StatusCode == 200 && getResponse.Text == "ok");

                var postResponse = tab.Post(baseUrl + "/", dict: new Dictionary<string, object> { { "a", "1" } });
                Assert("Tab.Post local http", postResponse.StatusCode == 200 && postResponse.Text == "posted");
            }
            finally
            {
                try
                {
                    tab.Navigate(restorePageUrl);
                    tab.WaitForDocumentComplete(CdpDocumentWaitScope.MainDocument, 10000);
                }
                catch
                {
                }

                StopHttpListener(listener);
            }
        }

        private static void StopHttpListener(HttpListener listener)
        {
            if (listener == null)
            {
                return;
            }

            try
            {
                listener.Stop();
                listener.Close();
            }
            catch
            {
            }
        }

        private static HttpListener StartLocalHttpServer()
        {
            var port = GetFreeTcpPort();
            var listener = new HttpListener();
            listener.Prefixes.Add(string.Format("http://127.0.0.1:{0}/", port));
            listener.Start();

            ThreadPool.QueueUserWorkItem(_ =>
            {
                while (listener.IsListening)
                {
                    HttpListenerContext context;
                    try
                    {
                        context = listener.GetContext();
                    }
                    catch (HttpListenerException)
                    {
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }

                    var body = context.Request.HttpMethod == "POST"
                        ? "posted"
                        : "ok";
                    var bytes = Encoding.UTF8.GetBytes(body);
                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "text/plain";
                    context.Response.ContentLength64 = bytes.Length;
                    context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                    context.Response.Close();
                }
            });

            return listener;
        }

        private static HttpListener StartNestedFramesHttpServer()
        {
            var port = GetFreeTcpPort();
            var listener = new HttpListener();
            listener.Prefixes.Add(string.Format("http://127.0.0.1:{0}/", port));
            listener.Start();

            ThreadPool.QueueUserWorkItem(_ =>
            {
                while (listener.IsListening)
                {
                    HttpListenerContext context;
                    try
                    {
                        context = listener.GetContext();
                    }
                    catch (HttpListenerException)
                    {
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }

                    try
                    {
                        ServeNestedFramesRequest(context);
                    }
                    catch
                    {
                        try
                        {
                            context.Response.StatusCode = 500;
                            context.Response.Close();
                        }
                        catch
                        {
                        }
                    }
                }
            });

            return listener;
        }

        private static void ServeNestedFramesRequest(HttpListenerContext context)
        {
            var path = context.Request.Url.AbsolutePath.TrimEnd('/');
            if (path.Length == 0)
            {
                path = "/";
            }

            string html = null;
            switch (path)
            {
                case "/nested-frames":
                    html = NestedFramesPages.Main;
                    break;
                case "/nested-frames/l1":
                    html = NestedFramesPages.L1;
                    break;
                case "/nested-frames/l2":
                    html = NestedFramesPages.L2;
                    break;
                case "/nested-frames/l3":
                    SleepQueryDelay(context.Request);
                    html = NestedFramesPages.L3;
                    break;
                case "/nested-frames/l3-v2":
                    html = NestedFramesPages.L3V2;
                    break;
            }

            if (html == null)
            {
                var notFound = Encoding.UTF8.GetBytes("Not Found");
                context.Response.StatusCode = 404;
                context.Response.ContentType = "text/plain; charset=utf-8";
                context.Response.ContentLength64 = notFound.Length;
                context.Response.OutputStream.Write(notFound, 0, notFound.Length);
                context.Response.Close();
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(html);
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            context.Response.Close();
        }

        private static void SleepQueryDelay(HttpListenerRequest request)
        {
            var raw = request.QueryString["delayMs"];
            int delayMs;
            if (!string.IsNullOrEmpty(raw) && int.TryParse(raw, out delayMs) && delayMs > 0)
            {
                Thread.Sleep(Math.Min(delayMs, 10000));
            }
        }

        private static int GetFreeTcpPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private void RunTabFindTests(CdpTab tab)
        {
            Section("Tab FindElement / FindElements / ElementExists");

            var input = tab.FindElement("<ctrl id=\"txt-input\" />", 5000);
            Assert("FindElement by id", input != null && input.Tag == "input");

            var items = tab.FindElements("<ctrl class=\"item\" />");
            Assert("FindElements count=3", items.Length == 3);

            Assert("ElementExists true", tab.ElementExists("<ctrl id=\"btn-click\" />"));
            Assert("ElementExists false", !tab.ElementExists("<ctrl id=\"not-exist-xyz\" />"));
            Assert("FindElement throwException false", tab.FindElement("<ctrl id=\"not-exist-xyz\" />", 0, false) == null);

            try
            {
                tab.FindElement("<ctrl id=\"not-exist-xyz\" />", 0, true);
                Fail("FindElement throwException true", "should throw");
            }
            catch (BrowserException)
            {
                Pass("FindElement throwException true");
            }

            try
            {
                tab.FindElement("<wnd title=\"x\" />", 0);
                Fail("FindElement rejects wnd", "should throw");
            }
            catch (InvalidOperationException)
            {
                Pass("FindElement rejects wnd");
            }
            catch (Exception ex)
            {
                Fail("FindElement rejects wnd", ex.Message);
            }
        }

        private void RunElementPropertyTests(CdpTab tab)
        {
            Section("Element properties");

            var input = tab.FindElement("<ctrl id=\"txt-input\" />", 0);
            Assert("Element.Tab same tab", ReferenceEquals(input.Tab, tab));
            Assert("Element.Tag", input.Tag == "input");
            Assert("Element.Attr id", input.Attr("id") == "txt-input");
            Assert("Element.Attr placeholder", input.Attr("placeholder") == "type here");
            Assert("Element.Value for input", input.Value == "hello");
            Assert("Element.Attrs contains id", input.Attrs.ContainsKey("id"));
            Assert("Element.Html contains input", input.Html.IndexOf("txt-input", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert("Element.InnerHtml empty-ish", input.InnerHtml == null || input.InnerHtml.Length >= 0);
            Assert("Element.States.IsDisplayed", input.States.IsDisplayed);
            Assert("Element.States.IsEnabled", input.States.IsEnabled);
            Assert("Element.States.IsAlive", input.States.IsAlive);
            Assert("Element.Rect.Size width > 0", input.Rect.Size.Item1 > 0);
            Assert("Element.Xpath not empty", !string.IsNullOrEmpty(input.Xpath));
            Assert("Element.CssSelector not empty", !string.IsNullOrEmpty(input.CssSelector));

            var link = tab.FindElement("<ctrl id=\"lnk-test\" />", 0);
            Assert("Element.Link", link.Link.IndexOf("example.com", StringComparison.OrdinalIgnoreCase) >= 0);

            var styled = tab.FindElement("<ctrl id=\"styled\" />", 0);
            Assert("Element.Style color", styled.Style("color").IndexOf("255", StringComparison.Ordinal) >= 0 ||
                   styled.Style("color").IndexOf("red", StringComparison.OrdinalIgnoreCase) >= 0);

            var container = tab.FindElement("<ctrl id=\"container\" />", 0);
            Assert("Element.ChildCount", container.ChildCount == 3);
            Assert("Element.Children length", container.Children().Length == 3);

            var nonSelect = tab.FindElement("<ctrl id=\"txt-input\" />", 0);
            Assert("IsMultiSelect false on input", !nonSelect.IsMultiSelect);
        }

        private void RunElementActionTests(CdpTab tab)
        {
            Section("Element actions (input/click/set/check)");

            var input = tab.FindElement("<ctrl id=\"txt-input\" />", 0);
            input.Clear();
            Assert("Clear input", input.Value == string.Empty);
            input.Input("world", clear: true);
            Assert("Input value", input.Value == "world");
            input.SetValue("set-value");
            Assert("SetValue", input.Value == "set-value");
            input.Focus();
            Pass("Focus");

            var btn = tab.FindElement("<ctrl id=\"btn-click\" />", 0);
            btn.Click();
            Thread.Sleep(100);
            var result = tab.FindElement("<ctrl id=\"click-result\" />", 0);
            Assert("Click handler", result.Text == "clicked");

            var chk = tab.FindElement("<ctrl id=\"chk-box\" />", 0);
            chk.Check();
            Assert("Check", chk.States.IsChecked);
            chk.Check(uncheck: true);
            Assert("Uncheck", !chk.States.IsChecked);

            var styled = tab.FindElement("<ctrl id=\"styled\" />", 0);
            styled.SetAttr("data-test", "ok");
            Assert("SetAttr", styled.Attr("data-test") == "ok");
            styled.SetStyle("color", "blue");
            Pass("SetStyle");
            styled.RemoveAttr("data-test");
            Assert("RemoveAttr", styled.Attr("data-test") == null);

            var host = tab.FindElement("<ctrl id=\"dynamic-host\" />", 0);
            host.SetInnerHtml("<span id=\"dyn-child\">dynamic</span>");
            var child = tab.FindElement("<ctrl id=\"dyn-child\" />", 2000);
            Assert("SetInnerHtml + find", child != null && child.Text == "dynamic");

            btn.Hover();
            Pass("Hover");
        }

        private void RunSelectTests(CdpTab tab)
        {
            Section("Select / Unselect / SelectAll / UnselectAll");

            var single = tab.FindElement("<ctrl id=\"sel-single\" />", 0);
            Assert("Single IsMultiSelect false", !single.IsMultiSelect);
            Assert("Single SelectOptions count", single.SelectOptions.Length == 3);
            Assert("Single initial selected Beta", single.SelectedOptions.Length == 1 && single.SelectedOptions[0].Text == "Beta");

            single.Select(CdpSelectBy.Text, "Alpha");
            Assert("Select by text", single.SelectedOptions[0].Text == "Alpha");
            single.Select(CdpSelectBy.Value, "c");
            Assert("Select by value", single.SelectedOptions[0].Attr("value") == "c");
            single.Select(CdpSelectBy.Index, 0);
            Assert("Select by index", single.SelectedOptions[0].Text == "Alpha");

            try
            {
                single.Select(CdpSelectBy.Text, "Alpha", "Beta");
                Fail("Single select multi-value throws", "should throw");
            }
            catch (BrowserException)
            {
                Pass("Single select multi-value throws");
            }

            var multi = tab.FindElement("<ctrl id=\"sel-multi\" />", 0);
            Assert("Multi IsMultiSelect true", multi.IsMultiSelect);
            multi.Select(CdpSelectBy.Value, "m1", "m3");
            Assert("Multi select two", multi.SelectedOptions.Length == 2);
            multi.Unselect(CdpSelectBy.Text, "One");
            Assert("Multi unselect one", multi.SelectedOptions.Length == 1 && multi.SelectedOptions[0].Text == "Three");
            multi.SelectAll();
            Assert("SelectAll", multi.SelectedOptions.Length == 4);
            multi.UnselectAll();
            Assert("UnselectAll", multi.SelectedOptions.Length == 0);
        }

        private void RunElementFindTests(CdpTab tab)
        {
            Section("Element-scoped FindElement / FindElements");

            var container = tab.FindElement("<ctrl id=\"container\" />", 0);
            var itemCount = Convert.ToInt32(container.RunJs("return this.querySelectorAll('.item').length;"));
            Assert("Container RunJs item count", itemCount == 3);

            var rows = container.FindElements("<ctrl class=\"item\" />");
            Assert("FindElements inside container", rows.Length == 3);

            var rowB = container.FindElement("<ctrl class=\"item\" idx=\"1\" />", 5000);
            Assert("FindElement inside container by idx", rowB != null && rowB.Text.Trim() == "Row B");

            var parentRow = rowB.FindElement("<parent level=\"1\" />", 0);
            Assert("FindElement parent", parentRow != null && parentRow.Attr("id") == "container");

            try
            {
                container.FindElement("<wnd title=\"x\" />", 0);
                Fail("Element FindElement rejects wnd", "should throw");
            }
            catch (InvalidOperationException)
            {
                Pass("Element FindElement rejects wnd");
            }
        }

        private void RunSendKeysTests(CdpTab tab)
        {
            Section("SendKeys");

            var input = tab.FindElement("<ctrl id=\"txt-input\" />", 0);
            input.Clear();
            input.Input("", clear: true);
            input.SendKeys(CdpKey.CtrlA);
            input.SendKeys(CdpKey.Backspace);
            input.SendKeys(CdpKey.Custom("X"));
            Assert("SendKeys char", input.Value == "X");
            input.SendKeys(CdpKey.CtrlA);
            input.SendKeys(CdpKey.Delete);
            Assert("SendKeys Ctrl+A Delete", input.Value == string.Empty);
        }

        private void RunScrollTests(CdpTab tab)
        {
            Section("Scroll");

            var box = tab.FindElement("<ctrl id=\"scroll-box\" />", 0);
            box.Scroll(CdpScrollDirection.Down, 50);
            Pass("Scroll down");
            box.ScrollToSee();
            Pass("ScrollToSee");
            box.ScrollToCenter();
            Pass("ScrollToCenter");
        }

        private void RunScreenshotTests(CdpTab tab)
        {
            Section("Screenshot");

            var viewportBytes = tab.GetScreenshot(fullPage: false);
            Assert("Tab viewport screenshot bytes", viewportBytes != null && viewportBytes.Length > 100);

            var viewportPath = Path.Combine(Path.GetTempPath(), "f2b-viewport-" + Guid.NewGuid().ToString("N") + ".png");
            tab.SaveScreenshot(viewportPath, fullPage: false);
            Assert("Tab viewport screenshot file", File.Exists(viewportPath) && new FileInfo(viewportPath).Length > 100);

            var fullPath = Path.Combine(Path.GetTempPath(), "f2b-full-" + Guid.NewGuid().ToString("N") + ".png");
            tab.SaveScreenshot(fullPath, fullPage: true);
            Assert("Tab full-page screenshot file", File.Exists(fullPath) && new FileInfo(fullPath).Length > 100);

            var button = tab.FindElement("<ctrl id=\"btn-click\" />", 0);
            var elementBytes = button.GetScreenshot();
            Assert("Element screenshot bytes", elementBytes != null && elementBytes.Length > 50);

            var elementPath = Path.Combine(Path.GetTempPath(), "f2b-element-" + Guid.NewGuid().ToString("N") + ".png");
            button.SaveScreenshot(elementPath);
            Assert("Element screenshot file", File.Exists(elementPath) && new FileInfo(elementPath).Length > 50);
        }

        private void RunParallelFindTests(CdpTab tab)
        {
            Section("ParallelFindElement");

            var selectors = new[]
            {
                "<ctrl id=\"not-exist-aaa\" />",
                "<ctrl id=\"not-exist-bbb\" />",
                "<ctrl id=\"btn-click\" />"
            };
            var result = tab.ParallelFindElement(selectors, 3000);
            Assert("ParallelFindElement index", result.Index == 2);
            Assert("ParallelFindElement element", result.Element != null && result.Element.Attr("id") == "btn-click");

            var none = tab.ParallelFindElement(new[] { "<ctrl id=\"nope1\" />", "<ctrl id=\"nope2\" />" }, 100);
            Assert("ParallelFindElement not found", !none.Found && none.Index == -1 && none.Element == null);
        }

        private void RunExtendedTests(CdpTab tab)
        {
            Section("Extended coverage");

            var title = tab.FindElement("<ctrl id=\"page-title\" />", 0);
            Assert("Element.Text on heading", title.Text.IndexOf("F2B CDP", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert("Element.InnerText", !string.IsNullOrEmpty(title.InnerText));
            Assert("Element.RawText", !string.IsNullOrEmpty(title.RawText));

            var input = tab.FindElement("<ctrl id=\"txt-input\" />", 0);
            Assert("Element.RunJs on element", Convert.ToString(input.RunJs("return this.id;")) == "txt-input");
            Assert("Element.RunJs args", Convert.ToInt32(input.RunJs("return arguments[0] * 2;", new object[] { 21 })) == 42);

            Assert("Element.Rect.Size", input.Rect.Size.Item1 > 0);
            Assert("Element.Rect.Midpoint", input.Rect.Midpoint.Item1 >= 0);
            Assert("Element.States.IsClickable", input.States.IsClickable);

            input.Input("js-mode", clear: true, method: CdpInteractionMethod.Js);
            Assert("Input Js method", input.Value == "js-mode");

            var area = tab.FindElement("<ctrl id=\"txt-area\" />", 0);
            Assert("Textarea value", area.Value == "area text");

            var single = tab.FindElement("<ctrl id=\"sel-single\" />", 0);
            single.Select(new object[] { "Gamma" }, CdpSelectBy.Text);
            Assert("Select IEnumerable overload", single.SelectedOptions[0].Text == "Gamma");

            Assert("FindElement with timeout", tab.FindElement("<ctrl id=\"btn-click\" />", 1000) != null);

            Assert("Tab.States.HasAlert false", !tab.States.HasAlert);

            var styled = tab.FindElement("<ctrl id=\"styled\" />", 0);
            Pass("Element.Pseudo access: " + (styled.Pseudo.Before ?? "(none)"));

            tab.WaitForDocumentComplete(CdpDocumentWaitScope.AllDocuments, 15000);
            const string frameInputSelector = "<frm id=\"inner-frame\" />\r\n<ctrl id=\"frame-input\" />";
            Assert("WaitForDocumentComplete AllDocuments + iframe ElementExists",
                tab.ElementExists(frameInputSelector));
            var frameInput = tab.FindElement(frameInputSelector, 0);
            Assert("FindElement in iframe after AllDocuments wait",
                frameInput != null && frameInput.Value == "inside-frame");

            var bodyEl = tab.FindElement("<ctrl tag=\"body\" />", 0);
            Assert("FindElement ctrl tag=body", bodyEl != null && bodyEl.Tag == "body");
            var hostIframe = bodyEl.FindElement("<frm id=\"inner-frame\" />", 0);
            Assert("FindElement terminal frm returns host iframe",
                hostIframe != null && hostIframe.Tag == "iframe");
            var frameFromBody = bodyEl.FindFrame("<frm id=\"inner-frame\" />", 0);
            Assert("FindFrame from body Element root",
                frameFromBody != null &&
                !string.IsNullOrEmpty(frameFromBody.FrameId) &&
                frameFromBody.Element != null &&
                frameFromBody.Element.Tag == "iframe");

            var frame = tab.FindFrame("<frm id=\"inner-frame\" />");
            Assert("FindFrame inner-frame succeeds", frame != null && !string.IsNullOrEmpty(frame.FrameId));
            Assert("Frame.RunJs document readyState",
                Convert.ToString(frame.RunJs("return document.readyState;")) == "complete");
            var frameScopedInput = frame.FindElement("<ctrl id=\"frame-input\" />", 0);
            Assert("Frame.FindElement frame-input",
                frameScopedInput != null && frameScopedInput.Value == "inside-frame");
            if (frame.Element != null)
            {
                var roundTrip = frame.Element.AsFrame();
                Assert("Element.AsFrame round-trip FrameId",
                    roundTrip != null && roundTrip.FrameId == frame.FrameId);
            }
            else
            {
                Fail("Frame.Element for AsFrame", "host element was null");
            }

            Assert("Element.Properties non-null dictionary",
                input.Properties != null && input.Properties.Count >= 0);
        }

        private static void WaitForPageReady(CdpTab tab)
        {
            tab.WaitForDocumentComplete(CdpDocumentWaitScope.MainDocument, 15000);
            if (!tab.ElementExists("<ctrl id=\"page-title\" />"))
            {
                throw new BrowserException("Test page marker element was not found.");
            }
        }

        private static void WaitForUrlReady(CdpTab tab, string urlPart, int timeoutMs)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(0, timeoutMs));
            while (DateTime.UtcNow < deadline)
            {
                if (tab.Url.IndexOf(urlPart, StringComparison.OrdinalIgnoreCase) >= 0 &&
                    tab.States.ReadyState == "complete")
                {
                    return;
                }

                Thread.Sleep(100);
            }

            throw new BrowserException(string.Format("Page url did not contain '{0}' in time.", urlPart));
        }

        private void Section(string name)
        {
            Console.WriteLine();
            Console.WriteLine("--- {0} ---", name);
        }

        private void Assert(string name, bool condition)
        {
            if (condition)
            {
                Pass(name);
            }
            else
            {
                Fail(name, "assertion failed");
            }
        }

        private void Pass(string name)
        {
            _passed++;
            Console.WriteLine("  [PASS] {0}", name);
        }

        private void Fail(string name, string detail)
        {
            _failed++;
            Console.WriteLine("  [FAIL] {0} - {1}", name, detail);
        }
    }
}
