using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FlaUI.Core.AutomationElements;
using FlaUI.Inspector.Models;

namespace FlaUI.Inspector.Services
{
    public static class SelectorResolveRetry
    {
        public const int DefaultTimeoutMilliseconds = 10_000;
        public const int DefaultIntervalMilliseconds = 250;

        /// <summary>
        /// 在超时内轮询解析 selector；唯一匹配立即返回，多匹配立即返回，零匹配则重试直至超时。
        /// </summary>
        public static async Task<IList<AutomationElement>> FindElementsWithRetryAsync(
            SelectorResolver resolver,
            IList<SelectorLevel> levels,
            int maxResults = 2,
            int timeoutMilliseconds = DefaultTimeoutMilliseconds,
            int intervalMilliseconds = DefaultIntervalMilliseconds,
            CancellationToken cancellationToken = default)
        {
            if (resolver == null)
                throw new ArgumentNullException(nameof(resolver));
            if (levels == null)
                throw new ArgumentNullException(nameof(levels));

            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
            IList<AutomationElement> lastResults = new List<AutomationElement>();

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    lastResults = resolver.FindElements(levels, maxResults);
                    if (lastResults.Count != 0)
                        return lastResults;
                }
                catch
                {
                    lastResults = new List<AutomationElement>();
                }

                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                    return lastResults;

                var delayMs = Math.Min(intervalMilliseconds, (int)Math.Ceiling(remaining.TotalMilliseconds));
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(true);
            }
        }
    }
}
