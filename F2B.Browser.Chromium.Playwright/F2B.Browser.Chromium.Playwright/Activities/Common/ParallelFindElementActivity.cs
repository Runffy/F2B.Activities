using System;
using System.Activities;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;

namespace F2B.Browser.Chromium.Playwright
{
    [DisplayName("Parallel Find Element")]
    [Description("Find the first selector that satisfies the specified wait state within timeout.")]
    [Designer(typeof(ParentSelectorActivityDesigner))]
    public sealed class ParallelFindElementActivity : CodeActivity
    {
        public ParallelFindElementActivity()
        {
            DisplayName = "Parallel Find Element";
        }

        private const int PollIntervalMs = 100;

        [DisplayName("Parent Object")]
        [Description("Root object for query. Accepts PwTab or PwElement.")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<object> ParentObject { get; set; }

        [DisplayName("Selectors")]
        [Description("Selector list as a VB.NET string array expression, e.g. New String() { \"...\" }. Edit via the activity designer ... button.")]
        [RequiredArgument]
        [Category("Input.B")]
        public InArgument<string[]> Selectors { get; set; }

        [DisplayName("Wait State")]
        [Description("Element state that must be satisfied.")]
        [Category("Input.C")]
        [DefaultValue(FindElementWaitState.None)]
        public FindElementWaitState WaitState { get; set; } = FindElementWaitState.None;

        [DisplayName("Timeout (ms)")]
        [Description("Timeout in milliseconds.")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int?> Timeout { get; set; } = 15000;

        [DisplayName("Matched Index")]
        [Description("Index of the first selector that satisfies wait state.")]
        [Category("Output")]
        public OutArgument<int> MatchedIndex { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var parent = ParentObject == null ? null : ParentObject.Get(context);
            var tab = parent as PwTab;
            var element = parent as PwElement;
            if (tab == null && element == null)
            {
                throw new InvalidOperationException("ParentObject must be PwTab or PwElement.");
            }

            var selectors = Selectors == null ? null : Selectors.Get(context);
            if (selectors == null || selectors.Length == 0)
            {
                throw new InvalidOperationException("Selectors is required and cannot be empty.");
            }

            var timeoutMs = ActivityArgumentHelper.GetOrDefault(Timeout, context, 15000);
            if (timeoutMs < 0)
            {
                timeoutMs = 0;
            }

            var waitState = WaitState;
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds <= timeoutMs)
            {
                for (var i = 0; i < selectors.Length; i++)
                {
                    var selector = selectors[i];
                    if (string.IsNullOrWhiteSpace(selector))
                    {
                        continue;
                    }

                    if (IsSelectorStateSatisfied(tab, element, selector, waitState))
                    {
                        MatchedIndex?.Set(context, i);
                        return;
                    }
                }

                if (sw.ElapsedMilliseconds > timeoutMs)
                {
                    break;
                }

                Thread.Sleep(PollIntervalMs);
            }

            throw new TimeoutException(
                $"ParallelFindElement timed out after {timeoutMs}ms. No selector satisfied state {waitState}.");
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);

            if (ParentObject == null || ParentObject.Expression == null)
            {
                metadata.AddValidationError("ParentObject is required.");
            }

            if (Selectors == null || Selectors.Expression == null)
            {
                metadata.AddValidationError("Selectors is required.");
            }
        }

        private static bool IsSelectorStateSatisfied(PwTab tab, PwElement element, string selector, FindElementWaitState waitState)
        {
            if (tab != null)
            {
                return tab.RunJs<bool>(
                    @"(arg) => {
                        const selector = arg.selector;
                        const state = arg.state;
                        const el = document.querySelector(selector);

                        const isVisible = (node) => {
                            if (!node) return false;
                            const style = window.getComputedStyle(node);
                            if (!style) return false;
                            if (style.display === 'none' || style.visibility === 'hidden' || style.visibility === 'collapse') return false;
                            if (parseFloat(style.opacity || '1') <= 0) return false;
                            return !!(node.offsetWidth || node.offsetHeight || node.getClientRects().length);
                        };

                        switch (state) {
                            case 'Visible': return isVisible(el);
                            case 'Hidden': return !el || !isVisible(el);
                            case 'Detached': return !el;
                            case 'Attached': return !!el;
                            case 'None':
                            default: return !!el;
                        }
                    }",
                    new { selector, state = waitState.ToString() });
            }

            return element.RunJs<bool>(
                @"(root, arg) => {
                    const selector = arg.selector;
                    const state = arg.state;
                    const el = root ? root.querySelector(selector) : null;

                    const isVisible = (node) => {
                        if (!node) return false;
                        const style = window.getComputedStyle(node);
                        if (!style) return false;
                        if (style.display === 'none' || style.visibility === 'hidden' || style.visibility === 'collapse') return false;
                        if (parseFloat(style.opacity || '1') <= 0) return false;
                        return !!(node.offsetWidth || node.offsetHeight || node.getClientRects().length);
                    };

                    switch (state) {
                        case 'Visible': return isVisible(el);
                        case 'Hidden': return !el || !isVisible(el);
                        case 'Detached': return !el;
                        case 'Attached': return !!el;
                        case 'None':
                        default: return !!el;
                    }
                }",
                new { selector, state = waitState.ToString() });
        }
    }
}
