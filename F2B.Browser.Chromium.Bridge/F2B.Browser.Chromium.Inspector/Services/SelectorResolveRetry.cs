using System;
using System.Threading;
using System.Threading.Tasks;
using F2B.Browser.Chromium.Bridge;
using F2B.Browser.Chromium.Bridge.Selectors;

namespace F2B.Browser.Chromium.Inspector.Services
{
    internal static class SelectorResolveRetry
    {
        public const int DefaultTimeoutMilliseconds = 5000;
        public const int DefaultIntervalMilliseconds = 250;

        /// <summary>
        /// 在超时内轮询 FindElements；唯一或多匹配立即返回，零匹配则重试直至超时。
        /// </summary>
        public static async Task<int> CountMatchesWithRetryAsync(
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

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    lastCount = client.FindElements(scope, tab).Length;
                    if (lastCount != 0)
                        return lastCount;
                }
                catch
                {
                    lastCount = 0;
                }

                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                    return lastCount;

                var delayMs = Math.Min(intervalMilliseconds, (int)Math.Ceiling(remaining.TotalMilliseconds));
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
