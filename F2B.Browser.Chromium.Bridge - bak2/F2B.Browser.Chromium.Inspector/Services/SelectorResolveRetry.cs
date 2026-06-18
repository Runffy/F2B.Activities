using System;
using System.Threading;
using System.Threading.Tasks;

namespace F2B.Browser.Chromium.Inspector.Services
{
    internal sealed class SelectorResolveResult
    {
        public static readonly SelectorResolveResult None = new SelectorResolveResult { Count = 0, Attempts = 0 };

        public int Count { get; set; }

        public int Attempts { get; set; }

        public string LastError { get; set; }
    }

    internal static class SelectorResolveRetry
    {
        public const int DefaultTimeoutMilliseconds = 5000;
        public const int DefaultIntervalMilliseconds = 300;

        /// <summary>
        /// FindElements is instantaneous; within the timeout window, poll every interval
        /// until count != 0, then return immediately.
        /// </summary>
        public static async Task<SelectorResolveResult> CountMatchesWithRetryAsync(
            Func<Task<int>> findElementsCountAsync,
            int timeoutMilliseconds = DefaultTimeoutMilliseconds,
            int intervalMilliseconds = DefaultIntervalMilliseconds,
            CancellationToken cancellationToken = default)
        {
            if (findElementsCountAsync == null)
                throw new ArgumentNullException(nameof(findElementsCountAsync));

            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
            var lastCount = 0;
            var attempts = 0;
            string lastError = null;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                attempts++;

                try
                {
                    lastCount = await findElementsCountAsync().ConfigureAwait(false);
                    lastError = null;
                    if (lastCount != 0)
                    {
                        return new SelectorResolveResult
                        {
                            Count = lastCount,
                            Attempts = attempts,
                            LastError = null
                        };
                    }
                }
                catch (Exception ex)
                {
                    lastCount = 0;
                    lastError = ex.Message;
                }

                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    return new SelectorResolveResult
                    {
                        Count = lastCount,
                        Attempts = attempts,
                        LastError = lastError
                    };
                }

                var delayMs = Math.Min(intervalMilliseconds, (int)Math.Ceiling(remaining.TotalMilliseconds));
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
