using System;
using System.Activities;
using System.Collections.Generic;
using System.ComponentModel;

namespace F2B.Browser.IExplore
{
    [DisplayName("Parallel Find Element")]
    [Description("Return index and element of the first matching locator within timeout.")]
    public sealed class ParallelFindElementActivity : CodeActivity
    {
        [DisplayName("IE Window")]
        [Category("Input")]
        public InArgument<EmbeddedIEWindow> InputWindow { get; set; }

        [DisplayName("Selectors (Json String)")]
        [Description("List of selector JSON strings.")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<List<string>> Selectors { get; set; }

        [DisplayName("Frame (Json String)")]
        [Description("Shared frame path for all locators (optional).")]
        [Category("Input")]
        public InArgument<string> FramePath { get; set; }

        [DisplayName("Timeout (ms)")]
        [Category("Input")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        [DisplayName("Matched Index")]
        [Category("Output")]
        public OutArgument<int> MatchedIndex { get; set; }

        [DisplayName("Matched Element")]
        [Category("Output")]
        public OutArgument<IEHtmlElement> MatchedElement { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var window = InputWindow == null ? null : InputWindow.Get(context);
            if (window == null)
                throw new InvalidOperationException("IE Window is required.");

            var list = Selectors == null ? null : Selectors.Get(context);
            if (list == null || list.Count == 0)
                throw new InvalidOperationException("Selectors (Json String) is required.");

            var locators = IeLocatorHelper.BuildLocators(list, IeLocatorHelper.GetFramePath(context, FramePath));
            var timeout = ActivityArgumentHelper.GetOrDefault(Timeout, context, OperationDefaults.TimeoutMs);
            var result = window.ParallelFindElement(locators, timeout);

            MatchedIndex?.Set(context, result.Index);
            MatchedElement?.Set(context, result.Element);
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);
            if (InputWindow == null || InputWindow.Expression == null)
                metadata.AddValidationError("IE Window is required.");
            if (Selectors == null || Selectors.Expression == null)
                metadata.AddValidationError("Selectors (Json String) is required.");
        }
    }
}
