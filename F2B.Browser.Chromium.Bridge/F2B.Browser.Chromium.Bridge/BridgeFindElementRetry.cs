using System;
using System.Threading;

namespace F2B.Browser.Chromium.Bridge
{
    internal static class BridgeFindElementRetry
    {
        public const int DefaultIntervalMs = 250;

        public static T Execute<T>(
            int timeoutMs,
            int delayBeforeMs,
            Func<int, int, T> attempt)
        {
            if (attempt == null)
                throw new ArgumentNullException(nameof(attempt));

            var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(0, timeoutMs));
            var delayApplied = false;
            Exception lastError = null;

            while (true)
            {
                var remainingMs = RemainingMs(deadline);
                if (remainingMs <= 0)
                    break;

                try
                {
                    var attemptDelay = delayApplied ? 0 : Math.Min(Math.Max(0, delayBeforeMs), remainingMs);
                    delayApplied = true;
                    return attempt(remainingMs, attemptDelay);
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }

                remainingMs = RemainingMs(deadline);
                if (remainingMs <= 0)
                    break;

                Thread.Sleep(Math.Min(DefaultIntervalMs, remainingMs));
            }

            throw CreateTimeoutException(timeoutMs, lastError);
        }

        private static int RemainingMs(DateTime deadline)
        {
            return Math.Max(0, (int)Math.Ceiling((deadline - DateTime.UtcNow).TotalMilliseconds));
        }

        private static TimeoutException CreateTimeoutException(int timeoutMs, Exception inner)
        {
            if (inner is TimeoutException timeoutException)
                return timeoutException;

            var message = inner == null || string.IsNullOrWhiteSpace(inner.Message)
                ? "FindElement failed within " + timeoutMs + " ms."
                : inner.Message;

            return new TimeoutException(message, inner);
        }
    }
}
