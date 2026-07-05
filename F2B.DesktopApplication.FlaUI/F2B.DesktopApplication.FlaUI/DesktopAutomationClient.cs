using System;
using System.Collections.Generic;
using System.Linq;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using F2B.DesktopApplication.FlaUI.Selectors;

namespace F2B.DesktopApplication.FlaUI
{
    public sealed class DesktopAutomationClient
    {
        private readonly SelectorResolver _selectorResolver;

        public DesktopAutomationClient()
            : this(AutomationContext.Instance.Automation)
        {
        }

        internal DesktopAutomationClient(UIA3Automation automation)
        {
            Automation = automation ?? throw new ArgumentNullException(nameof(automation));
            _selectorResolver = new SelectorResolver(Automation);
        }

        public UIA3Automation Automation { get; }

        public UiWindow FindWindow(
            string selectorXml,
            int timeoutMilliseconds = SelectorResolveRetry.DefaultTimeoutMilliseconds,
            int intervalMilliseconds = SelectorResolveRetry.DefaultIntervalMilliseconds)
        {
            var levels = ParseWindowSelector(selectorXml);
            IList<AutomationElement> matches;

            if (timeoutMilliseconds > 0)
            {
                matches = SelectorResolveRetry.FindElementsWithRetry(
                    _selectorResolver,
                    levels,
                    maxResults: 2,
                    timeoutMilliseconds,
                    intervalMilliseconds);
            }
            else
            {
                matches = _selectorResolver.FindElements(levels, maxResults: 2);
            }

            if (matches.Count == 0)
                throw new InvalidOperationException("No window matched the selector within the timeout.");

            if (matches.Count > 1)
                throw new InvalidOperationException("Window selector matched multiple windows.");

            return ToWindow(matches[0]);
        }

        public UiWindow[] FindWindows(string selectorXml)
        {
            var levels = ParseWindowSelector(selectorXml);
            return _selectorResolver.FindElements(levels)
                .Select(ToWindow)
                .ToArray();
        }

        public bool WindowExists(string selectorXml)
        {
            try
            {
                var levels = ParseWindowSelector(selectorXml);
                return _selectorResolver.FindElements(levels, maxResults: 1).Count > 0;
            }
            catch
            {
                return false;
            }
        }

        public void ActivateWindow(UiWindow window)
        {
            if (window == null)
                throw new ArgumentNullException(nameof(window));

            window.Activate();
        }

        public void ActivateWindow(string selectorXml, int timeoutMilliseconds = SelectorResolveRetry.DefaultTimeoutMilliseconds)
        {
            FindWindow(selectorXml, timeoutMilliseconds).Activate();
        }

        public void CloseWindow(UiWindow window)
        {
            if (window == null)
                throw new ArgumentNullException(nameof(window));

            window.Close();
        }

        public void CloseWindow(string selectorXml, int timeoutMilliseconds = SelectorResolveRetry.DefaultTimeoutMilliseconds)
        {
            FindWindow(selectorXml, timeoutMilliseconds).Close();
        }

        public UiElement FindElement(
            string selectorXml,
            int timeoutMilliseconds = SelectorResolveRetry.DefaultTimeoutMilliseconds,
            int intervalMilliseconds = SelectorResolveRetry.DefaultIntervalMilliseconds,
            UiWindow inputWindow = null)
        {
            var results = FindElements(selectorXml, maxResults: 2, timeoutMilliseconds, intervalMilliseconds, inputWindow);
            if (results.Count == 0)
                throw new InvalidOperationException("No element matched the selector within the timeout.");

            if (results.Count > 1)
                throw new InvalidOperationException("Selector matched multiple elements.");

            return results[0];
        }

        public IList<UiElement> FindElements(
            string selectorXml,
            int maxResults = int.MaxValue,
            int timeoutMilliseconds = 0,
            int intervalMilliseconds = SelectorResolveRetry.DefaultIntervalMilliseconds,
            UiWindow inputWindow = null)
        {
            if (string.IsNullOrWhiteSpace(selectorXml))
                throw new ArgumentException("Selector XML is required.", nameof(selectorXml));

            if (timeoutMilliseconds <= 0)
            {
                var levels = ParseElementSelector(selectorXml, inputWindow);
                var matches = _selectorResolver.FindElements(
                    levels,
                    inputWindow?.Element,
                    scopeFromProvidedRoot: inputWindow != null,
                    maxResults);
                return matches.Select(e => new UiElement(e, Automation)).ToList();
            }

            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);

            while (true)
            {
                try
                {
                    var levels = ParseElementSelector(selectorXml, inputWindow);
                    var matches = _selectorResolver.FindElements(
                        levels,
                        inputWindow?.Element,
                        scopeFromProvidedRoot: inputWindow != null,
                        maxResults);

                    if (matches.Count != 0)
                        return matches.Select(e => new UiElement(e, Automation)).ToList();
                }
                catch
                {
                }

                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                    return new List<UiElement>();

                var delayMs = Math.Min(intervalMilliseconds, (int)Math.Ceiling(remaining.TotalMilliseconds));
                System.Threading.Thread.Sleep(delayMs);
            }
        }

        public bool ElementExists(string selectorXml, UiWindow inputWindow = null)
        {
            try
            {
                var levels = ParseElementSelector(selectorXml, inputWindow);
                return _selectorResolver.FindElements(
                    levels,
                    inputWindow?.Element,
                    scopeFromProvidedRoot: inputWindow != null,
                    maxResults: 1).Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private static IList<SelectorLevel> ParseWindowSelector(string selectorXml)
        {
            var levels = SelectorScopeHelper.Parse(selectorXml);
            SelectorScopeHelper.EnsureWindowOnly(levels);
            return levels;
        }

        private static IList<SelectorLevel> ParseElementSelector(string selectorXml, UiWindow inputWindow)
        {
            var levels = SelectorScopeHelper.Parse(selectorXml);
            if (inputWindow != null)
                SelectorScopeHelper.EnsureControlOnly(levels);
            return levels;
        }

        private UiWindow ToWindow(AutomationElement element)
        {
            return new UiWindow(element, Automation);
        }
    }
}
