using System;
using System.Threading;

namespace F2B.Browser.Chromium.Playwright
{
    internal static class PlaywrightFindElementRetry
    {
        public const int DefaultIntervalMs = 250;

        public static T Execute<T>(
            double timeoutMs,
            int delayBeforeMs,
            Func<double, int, T> attempt)
        {
            if (attempt == null)
                throw new ArgumentNullException(nameof(attempt));

            if (timeoutMs <= 0)
                return attempt(0, delayBeforeMs);

            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
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

        private static TimeoutException CreateTimeoutException(double timeoutMs, Exception inner)
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
