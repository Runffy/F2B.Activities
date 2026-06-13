using System;
using System.Activities;
using System.Collections.Generic;
using System.ComponentModel;
using System.Activities.Presentation;

namespace F2B.Browser.Chromium.Bridge
{
    [DisplayName("Parallel Find Element")]
    [Description("Find the first matching selector within timeout.")]
    [Designer(typeof(BridgeParentSelectorActivityDesigner))]
    public sealed class ParallelFindElementActivity : CodeActivity
    {
        public ParallelFindElementActivity()
        {
            DisplayName = "Parallel Find Element";
        }

        [DisplayName("Parent Object")]
        [Description("Root object for query. Accepts BwTab or BwElement.")]
        [RequiredArgument]
        [Category("Input.A")]
        public InArgument<object> ParentObject { get; set; }

        [DisplayName("Selectors")]
        [RequiredArgument]
        [Category("Input.B")]
        public InArgument<List<string>> Selectors { get; set; }

        [DisplayName("Wait State")]
        [Category("Input.C")]
        [DefaultValue(BridgeFindElementWaitState.None)]
        public BridgeFindElementWaitState WaitState { get; set; } = BridgeFindElementWaitState.None;

        [DisplayName("Timeout (ms)")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        [DisplayName("Matched Index")]
        [Category("Output")]
        public OutArgument<int> MatchedIndex { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var parent = ParentObject == null ? null : ParentObject.Get(context);
            var tab = parent as BwTab;
            var element = parent as BwElement;
            if (tab == null && element == null)
                throw new InvalidOperationException("ParentObject must be BwTab or BwElement.");

            var selectors = Selectors == null ? null : Selectors.Get(context);
            if (selectors == null || selectors.Count == 0)
                throw new InvalidOperationException("Selectors is required and cannot be empty.");

            var timeoutMs = BridgeActivityArgumentHelper.GetOrDefault(Timeout, context, 15000);
            int idx;
            if (tab != null)
                idx = tab.ParallelFindElement(selectors, timeoutMs, WaitState);
            else
                idx = element.ParallelFindElement(selectors, timeoutMs, WaitState);

            if (idx < 0)
                throw new TimeoutException("ParallelFindElement timed out. No selector matched.");

            MatchedIndex?.Set(context, idx);
        }
    }
}
