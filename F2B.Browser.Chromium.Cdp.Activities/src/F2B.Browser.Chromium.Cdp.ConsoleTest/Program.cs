using System;
using F2B.Browser.Chromium.Cdp;
using F2B.Browser.Chromium.Cdp.Browser;
using F2B.Browser.Chromium.Cdp.Exceptions;

namespace F2B.Browser.Chromium.Cdp.ConsoleTest
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return 1;
            }

            try
            {
                switch (args[0].ToLowerInvariant())
                {
                    case "open":
                        return RunOpen(args);
                    case "find-tab":
                        return RunFindTab(args);
                    case "check-port":
                        return RunCheckPort(args);
                    case "test-all":
                        return RunTestAll(args);
                    default:
                        Console.WriteLine("Unknown command: {0}", args[0]);
                        PrintUsage();
                        return 1;
                }
            }
            catch (BrowserException ex)
            {
                Console.WriteLine("[BrowserException] {0}", ex.Message);
                return 2;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Error] {0}", ex);
                return 3;
            }
        }

        private static int RunTestAll(string[] args)
        {
            var port = GetIntArg(args, "--port", 0);
            var keepBrowser = GetBoolArg(args, "--keep-browser", false);
            var attachOnly = GetBoolArg(args, "--attach", false);

            CdpBrowser browser;
            if (attachOnly && port > 0)
            {
                Console.WriteLine("Attaching to browser on port {0}...", port);
                browser = CdpBrowser.Attach(port);
            }
            else
            {
                var options = new BrowserOpenOptions
                {
                    Port = port,
                    Force = GetBoolArg(args, "--force", true),
                    UserDataDir = GetStringArg(args, "--user-data-dir", "temp"),
                    ExecutablePath = GetStringArg(args, "--executable-path", "chrome"),
                    StartArguments = GetStringArg(args, "--start-arguments", null)
                };

                Console.WriteLine("Opening browser for full feature test...");
                Console.WriteLine("  port          = {0}", options.Port <= 0 ? "auto" : options.Port.ToString());
                Console.WriteLine("  user_data_dir = {0}", options.UserDataDir);
                browser = ChromiumBrowser.OpenBrowser(options);
            }

            Console.WriteLine("  resolved port = {0}", browser.Port);
            Console.WriteLine();

            try
            {
                return new CdpFeatureTests(browser, keepBrowser).RunAll();
            }
            finally
            {
                if (!keepBrowser)
                {
                    try
                    {
                        browser.Quit();
                    }
                    catch
                    {
                    }

                    browser.Dispose();
                }
            }
        }

        private static int RunOpen(string[] args)
        {
            var options = new BrowserOpenOptions
            {
                Port = GetIntArg(args, "--port", 0),
                Force = GetBoolArg(args, "--force", false),
                UserDataDir = GetStringArg(args, "--user-data-dir", "temp"),
                ExecutablePath = GetStringArg(args, "--executable-path", "chrome"),
                StartArguments = GetStringArg(args, "--start-arguments", null)
            };

            Console.WriteLine("Opening browser...");
            Console.WriteLine("  executable_path = {0}", options.ExecutablePath);
            Console.WriteLine("  port            = {0}", options.Port <= 0 ? "auto" : options.Port.ToString());
            Console.WriteLine("  user_data_dir   = {0}", options.UserDataDir);
            Console.WriteLine("  force           = {0}", options.Force);
            Console.WriteLine("  start_arguments = {0}", options.StartArguments ?? "(none)");

            var browser = ChromiumBrowser.OpenBrowser(options);

            Console.WriteLine();
            Console.WriteLine("OpenBrowser succeeded.");
            Console.WriteLine("  resolved executable = {0}", browser.ExecutablePath);
            Console.WriteLine("  resolved port       = {0}", browser.Port);
            Console.WriteLine("  resolved user_data  = {0}", browser.UserDataDir);
            Console.WriteLine("  browser_name        = {0}", browser.BrowserName);
            Console.WriteLine("  attached_existing   = {0}", browser.AttachedToExisting);
            Console.WriteLine("  process_id          = {0}",
                browser.Process == null ? "(attached/none)" : browser.Process.Id.ToString());
            Console.WriteLine("  cdp_endpoint        = http://127.0.0.1:{0}/json/version", browser.Port);
            Console.WriteLine("  tabs_count          = {0}", browser.TabsCount);
            Console.WriteLine("  visible_tabs        = {0}", browser.GetTabs().Count);
            Console.WriteLine("  latest_tab          = {0}", browser.LatestTab);

            if (GetBoolArg(args, "--tab-test", false))
            {
                RunTabSmokeTest(browser);
            }

            return 0;
        }

        private static void RunTabSmokeTest(CdpBrowser browser)
        {
            Console.WriteLine();
            Console.WriteLine("Running tab smoke test...");

            var newTab = browser.NewTab("https://example.com", background: true);
            Console.WriteLine("  new_tab             = {0}", newTab);

            browser.ActivateTab(newTab);
            Console.WriteLine("  activated new tab");

            var matched = browser.GetTab(url: "example.com");
            Console.WriteLine("  get_tab by url      = {0}", matched);

            var allTabs = browser.GetTabs();
            Console.WriteLine("  get_tabs count      = {0}", allTabs.Count);

            var findResult = CdpTabFinder.FindTab(
                string.Format("<wnd url=\"{0}\" port=\"{1}\" browser=\"{2}\" />", newTab.Url, browser.Port, browser.BrowserName));
            Console.WriteLine("  find_tab            = {0}", findResult.Tab);

            var findIdx0 = CdpTabFinder.FindTab(
                string.Format("<wnd port=\"{0}\" browser=\"{1}\" idx=\"0\" />", browser.Port, browser.BrowserName));
            var findIdx1 = CdpTabFinder.FindTab(
                string.Format("<wnd port=\"{0}\" browser=\"{1}\" idx=\"1\" />", browser.Port, browser.BrowserName));
            Console.WriteLine("  find_tab idx=0       = {0}", findIdx0.Tab);
            Console.WriteLine("  find_tab idx=1       = {0}", findIdx1.Tab);

            browser.CloseTab(newTab);
            Console.WriteLine("  closed new tab, tabs_count = {0}", browser.TabsCount);
        }

        private static int RunFindTab(string[] args)
        {
            var selector = GetStringArg(args, "--selector", null);
            if (string.IsNullOrWhiteSpace(selector))
            {
                Console.WriteLine("Missing required argument: --selector \"<wnd ... />\"");
                return 1;
            }

            var result = CdpTabFinder.FindTab(selector);
            Console.WriteLine("FindTab succeeded.");
            Console.WriteLine("  browser_port  = {0}", result.Browser.Port);
            Console.WriteLine("  browser_name  = {0}", result.Browser.BrowserName);
            Console.WriteLine("  tab           = {0}", result.Tab);
            return 0;
        }

        private static int RunCheckPort(string[] args)
        {
            var port = GetIntArg(args, "--port", 9222);
            Console.WriteLine("Use 'open --port {0}' to verify CDP connectivity.", port);
            return 0;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  F2B.Browser.Chromium.Cdp.ConsoleTest test-all [--port N] [--force] [--user-data-dir VALUE] [--executable-path VALUE] [--keep-browser]");
            Console.WriteLine("  F2B.Browser.Chromium.Cdp.ConsoleTest open [--port N] [--force] [--user-data-dir VALUE] [--executable-path VALUE] [--start-arguments \"...\"] [--tab-test]");
            Console.WriteLine("  F2B.Browser.Chromium.Cdp.ConsoleTest find-tab --selector \"<wnd title='...' />\"");
            Console.WriteLine("  F2B.Browser.Chromium.Cdp.ConsoleTest check-port [--port N]");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  test-all --port 0 --user-data-dir temp");
            Console.WriteLine("  open --port 9222 --user-data-dir temp");
        }

        private static string GetStringArg(string[] args, string name, string defaultValue)
        {
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return defaultValue;
        }

        private static int GetIntArg(string[] args, string name, int defaultValue)
        {
            var value = GetStringArg(args, name, null);
            int parsed;
            return value != null && int.TryParse(value, out parsed) ? parsed : defaultValue;
        }

        private static bool GetBoolArg(string[] args, string name, bool defaultValue)
        {
            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return defaultValue;
        }
    }
}
