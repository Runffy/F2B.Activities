using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Automation;

namespace F2B.Browser.Chromium.Playwright
{
    public enum ChromiumBrowserKind
    {
        Edge,
        Chrome
    }

    public static class BrowserWindowCloser
    {
        private const int SoftCloseTimeoutMs = 15000;
        private const int SoftCloseDelayAfterMs = 500;

        public static ChromiumBrowserKind ResolveBrowserKind(string browserPath)
        {
            var executableName = Path.GetFileName(browserPath ?? string.Empty).ToLowerInvariant();
            if (executableName == "chrome.exe" ||
                (executableName.Contains("chrome") && !executableName.Contains("edge")))
            {
                return ChromiumBrowserKind.Chrome;
            }

            return ChromiumBrowserKind.Edge;
        }

        public static string GetProcessName(ChromiumBrowserKind browserKind)
        {
            return browserKind == ChromiumBrowserKind.Chrome ? "chrome" : "msedge";
        }

        public static string GetWindowTitleSuffix(ChromiumBrowserKind browserKind)
        {
            return browserKind == ChromiumBrowserKind.Chrome
                ? " - Google Chrome"
                : " - Microsoft Edge";
        }

        public static List<int> GetRunningPids(ChromiumBrowserKind browserKind)
        {
            try
            {
                return new List<int>(
                    Array.ConvertAll(
                        Process.GetProcessesByName(GetProcessName(browserKind)),
                        process => process.Id));
            }
            catch
            {
                return new List<int>();
            }
        }

        public static int CleanupBrowserProcesses(ChromiumBrowserKind browserKind, int delayAfterKillMs)
        {
            SoftCloseAllWindows(browserKind);

            var killedCount = KillAllProcesses(browserKind);
            if (delayAfterKillMs > 0)
            {
                Thread.Sleep(delayAfterKillMs);
            }

            return killedCount;
        }

        public static int SoftCloseAllWindows(ChromiumBrowserKind browserKind, int timeoutMs = SoftCloseTimeoutMs)
        {
            if (timeoutMs < 0)
            {
                timeoutMs = 0;
            }

            var titleSuffix = GetWindowTitleSuffix(browserKind);
            var stopwatch = Stopwatch.StartNew();
            var closedCount = 0;

            while (stopwatch.ElapsedMilliseconds <= timeoutMs)
            {
                var windows = FindBrowserWindows(titleSuffix);
                if (windows.Count == 0)
                {
                    return closedCount;
                }

                foreach (var window in windows)
                {
                    var remainingMs = timeoutMs - (int)stopwatch.ElapsedMilliseconds;
                    if (remainingMs <= 0)
                    {
                        break;
                    }

                    try
                    {
                        SoftCloseWindow(window, Math.Min(10000, remainingMs));
                        closedCount++;
                    }
                    catch
                    {
                    }
                }

                if (SoftCloseDelayAfterMs > 0 && stopwatch.ElapsedMilliseconds < timeoutMs)
                {
                    Thread.Sleep(SoftCloseDelayAfterMs);
                }

                if (FindBrowserWindows(titleSuffix).Count == 0)
                {
                    return closedCount;
                }
            }

            return closedCount;
        }

        public static int KillAllProcesses(ChromiumBrowserKind browserKind)
        {
            var killed = 0;
            try
            {
                foreach (var process in Process.GetProcessesByName(GetProcessName(browserKind)))
                {
                    try
                    {
                        process.Kill();
                        killed++;
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

            return killed;
        }

        private static List<AutomationElement> FindBrowserWindows(string titleSuffix)
        {
            var result = new List<AutomationElement>();
            var root = AutomationElement.RootElement;
            var condition = new PropertyCondition(
                AutomationElement.ControlTypeProperty,
                ControlType.Window);

            foreach (AutomationElement window in root.FindAll(TreeScope.Children, condition))
            {
                try
                {
                    var name = window.Current.Name;
                    if (!string.IsNullOrEmpty(name) &&
                        name.EndsWith(titleSuffix, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add(window);
                    }
                }
                catch
                {
                }
            }

            return result;
        }

        private static void SoftCloseWindow(AutomationElement window, int waitTimeoutMs)
        {
            WaitForWindowReady(window, waitTimeoutMs);

            try
            {
                window.SetFocus();
            }
            catch
            {
            }

            if (window.TryGetCurrentPattern(WindowPattern.Pattern, out var patternObj))
            {
                ((WindowPattern)patternObj).Close();
                return;
            }

            throw new InvalidOperationException("The browser window does not support WindowPattern.Close.");
        }

        private static void WaitForWindowReady(AutomationElement window, int waitTimeoutMs)
        {
            if (waitTimeoutMs < 0)
            {
                waitTimeoutMs = 0;
            }

            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.ElapsedMilliseconds <= waitTimeoutMs)
            {
                try
                {
                    if (window.Current.IsEnabled)
                    {
                        return;
                    }
                }
                catch (ElementNotAvailableException)
                {
                    return;
                }

                Thread.Sleep(100);
            }
        }
    }
}
