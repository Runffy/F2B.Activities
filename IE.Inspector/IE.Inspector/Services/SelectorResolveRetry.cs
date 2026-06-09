using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using F2B.Browser.IExplore.COM;
using IE.Inspector.Models;

namespace IE.Inspector.Services
{
    public static class SelectorResolveRetry
    {
        public const int DefaultTimeoutMilliseconds = 10_000;
        public const int DefaultIntervalMilliseconds = 250;

        public static async Task<IList<IEWindowController.IEDomElement>> FindElementsWithRetryAsync(
            SelectorResolver resolver,
            IList<SelectorLevel> levels,
            int maxResults = 2,
            int timeoutMilliseconds = DefaultTimeoutMilliseconds,
            int intervalMilliseconds = DefaultIntervalMilliseconds,
            CancellationToken cancellationToken = default)
        {
            if (resolver == null)
                throw new ArgumentNullException(nameof(resolver));

            var levelList = levels ?? new List<SelectorLevel>();
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
            IList<IEWindowController.IEDomElement> lastResults = new List<IEWindowController.IEDomElement>();

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    lastResults = resolver.FindElements(levelList, maxResults: maxResults);
                    if (lastResults.Count != 0)
                        return lastResults;
                }
                catch
                {
                    lastResults = new List<IEWindowController.IEDomElement>();
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
