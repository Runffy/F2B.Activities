using System;
using System.Threading;
using System.Threading.Tasks;
using F2B.Browser.Chromium.Bridge;
using F2B.Browser.Chromium.Bridge.Selectors;

namespace F2B.Browser.Chromium.Inspector.Services
{
    internal sealed class SelectorResolveResult
    {
        public int Count { get; set; }

        public int Attempts { get; set; }

        public string LastError { get; set; }
    }

    internal static class SelectorResolveRetry
    {
        public const int DefaultTimeoutMilliseconds = 5000;
        public const int DefaultIntervalMilliseconds = 250;

        public static async Task<int> CountMatchesWithRetryAsync(
            BridgeSyncClient client,
            SelectorScope scope,
            BwTab tab,
            int timeoutMilliseconds = DefaultTimeoutMilliseconds,
            int intervalMilliseconds = DefaultIntervalMilliseconds,
            CancellationToken cancellationToken = default)
        {
            var result = await CountMatchesDetailedAsync(
                client,
                scope,
                tab,
                timeoutMilliseconds,
                intervalMilliseconds,
                cancellationToken).ConfigureAwait(false);

            return result.Count;
        }

        public static async Task<SelectorResolveResult> CountMatchesDetailedAsync(
            BridgeSyncClient client,
            SelectorScope scope,
            BwTab tab,
            int timeoutMilliseconds = DefaultTimeoutMilliseconds,
            int intervalMilliseconds = DefaultIntervalMilliseconds,
            CancellationToken cancellationToken = default)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));
            if (scope == null)
                throw new ArgumentNullException(nameof(scope));
            if (tab == null)
                throw new ArgumentNullException(nameof(tab));

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
                    lastCount = client.FindElements(scope, tab).Length;
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
