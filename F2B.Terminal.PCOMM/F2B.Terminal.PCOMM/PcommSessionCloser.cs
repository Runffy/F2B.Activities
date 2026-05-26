using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Automation;

namespace F2B.Terminal.PCOMM
{
    public static class PcommSessionCloser
    {
        private static readonly Regex SessionTitlePattern = new Regex(
            @"Session [A-Z]",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static int SoftCloseAllSessions(int timeoutMs = 15000, int delayAfterMs = 2000)
        {
            if (timeoutMs < 0)
            {
                timeoutMs = 0;
            }

            if (delayAfterMs < 0)
            {
                delayAfterMs = 0;
            }

            var stopwatch = Stopwatch.StartNew();
            var closedCount = 0;

            while (stopwatch.ElapsedMilliseconds <= timeoutMs)
            {
                var windows = FindSessionWindows();
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
                        // Retry remaining windows within the timeout window.
                    }
                }

                if (delayAfterMs > 0 && stopwatch.ElapsedMilliseconds < timeoutMs)
                {
                    Thread.Sleep(delayAfterMs);
                }

                if (FindSessionWindows().Count == 0)
                {
                    return closedCount;
                }
            }

            var remainingWindows = FindSessionWindows();
            if (remainingWindows.Count > 0)
            {
                throw new TimeoutException(
                    "Timed out after " + timeoutMs + " ms. Remaining PCOMM session windows: " +
                    remainingWindows.Count + ".");
            }

            return closedCount;
        }

        private static List<AutomationElement> FindSessionWindows()
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
                    if (!string.IsNullOrEmpty(name) && SessionTitlePattern.IsMatch(name))
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

            throw new InvalidOperationException("The session window does not support WindowPattern.Close.");
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
