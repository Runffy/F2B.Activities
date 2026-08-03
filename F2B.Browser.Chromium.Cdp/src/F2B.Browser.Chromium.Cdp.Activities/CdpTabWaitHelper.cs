using System;
using System.Threading;
using F2B.Browser.Chromium.Cdp.Browser;
using F2B.Browser.Chromium.Cdp.Exceptions;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    internal static class CdpTabWaitHelper
    {
        public static void WaitForReadyState(CdpTab tab, CdpWaitForState waitForState, int timeoutMs)
        {
            if (tab == null)
            {
                throw new ArgumentNullException("tab");
            }

            if (timeoutMs <= 0)
            {
                return;
            }

            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            do
            {
                if (IsReadyStateReached(tab, waitForState))
                {
                    return;
                }

                Thread.Sleep(50);
            }
            while (DateTime.UtcNow < deadline);

            throw new BrowserException(
                string.Format(
                    "Tab did not reach ready state '{0}' within {1} ms. Current state: {2}.",
                    waitForState,
                    timeoutMs,
                    tab.States.ReadyState));
        }

        private static bool IsReadyStateReached(CdpTab tab, CdpWaitForState waitForState)
        {
            var current = tab.States.ReadyState ?? string.Empty;
            var currentOrder = GetReadyStateOrder(current);
            var targetOrder = GetReadyStateOrder(waitForState);

            if (currentOrder < targetOrder)
            {
                return false;
            }

            if (waitForState == CdpWaitForState.Complete)
            {
                return string.Equals(current, "complete", StringComparison.OrdinalIgnoreCase) &&
                       !tab.States.IsLoading;
            }

            return true;
        }

        private static int GetReadyStateOrder(CdpWaitForState state)
        {
            switch (state)
            {
                case CdpWaitForState.Connecting:
                    return 0;
                case CdpWaitForState.Loading:
                    return 1;
                case CdpWaitForState.Interactive:
                    return 2;
                case CdpWaitForState.Complete:
                    return 3;
                default:
                    return -1;
            }
        }

        private static int GetReadyStateOrder(string readyState)
        {
            if (string.Equals(readyState, "connecting", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (string.Equals(readyState, "loading", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (string.Equals(readyState, "interactive", StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            if (string.Equals(readyState, "complete", StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }

            return -1;
        }
    }
}
