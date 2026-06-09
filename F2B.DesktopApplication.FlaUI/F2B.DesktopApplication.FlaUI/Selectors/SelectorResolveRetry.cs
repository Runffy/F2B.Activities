using System;
using System.Collections.Generic;
using System.Threading;
using FlaUI.Core.AutomationElements;

namespace F2B.DesktopApplication.FlaUI.Selectors
{
    public static class SelectorResolveRetry
    {
        public const int DefaultTimeoutMilliseconds = 10_000;
        public const int DefaultIntervalMilliseconds = 250;

        public static IList<AutomationElement> FindElementsWithRetry(
            SelectorResolver resolver,
            string selectorXml,
            int maxResults = 2,
            int timeoutMilliseconds = DefaultTimeoutMilliseconds,
            int intervalMilliseconds = DefaultIntervalMilliseconds,
            CancellationToken cancellationToken = default)
        {
            if (resolver == null)
                throw new ArgumentNullException(nameof(resolver));
            if (string.IsNullOrWhiteSpace(selectorXml))
                return new List<AutomationElement>();

            var levels = SelectorXmlSerializer.Deserialize(selectorXml);
            return FindElementsWithRetry(
                resolver,
                levels,
                maxResults,
                timeoutMilliseconds,
                intervalMilliseconds,
                searchRoot: null,
                scopeFromProvidedRoot: false,
                cancellationToken: cancellationToken);
        }

        public static IList<AutomationElement> FindElementsWithRetry(
            SelectorResolver resolver,
            IList<SelectorLevel> levels,
            int maxResults = 2,
            int timeoutMilliseconds = DefaultTimeoutMilliseconds,
            int intervalMilliseconds = DefaultIntervalMilliseconds,
            AutomationElement searchRoot = null,
            bool scopeFromProvidedRoot = false,
            CancellationToken cancellationToken = default)
        {
            if (resolver == null)
                throw new ArgumentNullException(nameof(resolver));

            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
            IList<AutomationElement> lastResults = new List<AutomationElement>();

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    lastResults = resolver.FindElements(levels, searchRoot, scopeFromProvidedRoot, maxResults);
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
                Thread.Sleep(delayMs);
            }
        }
    }
}
