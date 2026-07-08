using System;
using System.Activities;
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
        [Description("Root object for query. Accepts BwTab or BwElement. Optional when every selector contains <wnd>.")]
        [Category("Input.A")]
        public InArgument<object> ParentObject { get; set; }

        [DisplayName("Selectors")]
        [Description("Selector XML list as a VB.NET string array expression, e.g. New String() { \"...\" }. Edit via the activity designer ... button.")]
        [RequiredArgument]
        [Category("Input.B")]
        public InArgument<string[]> Selectors { get; set; }

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
            if (parent != null && tab == null && element == null)
            {
                throw new InvalidOperationException("ParentObject must be BwTab or BwElement when provided.");
            }

            var selectors = Selectors == null ? null : Selectors.Get(context);
            if (selectors == null || selectors.Length == 0)
            {
                throw new InvalidOperationException("Selectors is required and cannot be empty.");
            }

            var timeoutMs = BridgeActivityArgumentHelper.GetOrDefault(Timeout, context, 15000);
            var idx = BridgeParallelFindHelper.Find(tab, element, selectors, timeoutMs, WaitState);
            if (idx < 0)
            {
                throw new TimeoutException("ParallelFindElement timed out. No selector matched.");
            }

            MatchedIndex?.Set(context, idx);
        }
    }
}
