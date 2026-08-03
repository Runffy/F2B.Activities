using System;
using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;
using F2B.Browser.Chromium.Cdp.Browser;
using F2B.Browser.Chromium.Cdp.Internal;
using F2B.Browser.Chromium.Cdp.Selectors;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("ParallelFindElement")]
    [Description("Poll multiple selector XML strings in parallel and return the first match.")]
    [Designer(typeof(CdpParallelFindElementActivityDesigner))]
    public sealed class ParallelFindElementActivity : CodeActivity
    {
        public ParallelFindElementActivity()
        {
            DisplayName = "ParallelFindElement";
        }

        [DisplayName("Parent Object")]
        [Description("Optional common search root. When empty, each selector must contain <wnd>.")]
        [Category("Input.A")]
        public InArgument<CdpBase> ParentObject { get; set; }

        [DisplayName("Selectors")]
        [Description("Selector XML list as a VB.NET string array expression, e.g. New String() { \"...\" }.")]
        [RequiredArgument]
        [Category("Input.B")]
        public InArgument<string[]> Selectors { get; set; }

        [DisplayName("Timeout (ms)")]
        [Description("Maximum wait time in milliseconds.")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        [DisplayName("Result")]
        [Description("Outputs the first matched selector index and element.")]
        [Category("Output")]
        public OutArgument<CdpParallelFindElementResult> Result { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var selectors = Selectors == null ? null : Selectors.Get(context);
            if (selectors == null || selectors.Length == 0)
            {
                throw new InvalidOperationException("Selectors is required and cannot be empty.");
            }

            var root = CdpTargetResolver.GetRoot(ParentObject, context, "ParentObject");
            if (root == null)
            {
                foreach (var item in selectors)
                {
                    if (!SelectorXmlSerializer.HasWndLevel(item))
                    {
                        throw new InvalidOperationException(
                            "Provide ParentObject, or ensure every Selector contains <wnd>.");
                    }
                }
            }

            var timeoutMs = CdpActivityArgumentHelper.GetOrDefault(Timeout, context, 15000);
            CdpParallelFindElementResult result;
            if (root != null)
            {
                // Align with Find*: when ParentObject is set, strip leading <wnd> so relative
                // and accidental full-selector expressions both work under a bound root.
                var normalized = new string[selectors.Length];
                for (var i = 0; i < selectors.Length; i++)
                {
                    normalized[i] = CdpTargetResolver.NormalizeSelector(root, selectors[i]);
                }

                result = SelectorElementFinder.ParallelFindElement(root, normalized, timeoutMs);
            }
            else
            {
                result = ParallelFindWithWndSelectors(selectors, timeoutMs);
            }

            if (!result.Found)
            {
                throw new TimeoutException("ParallelFindElement timed out. No selector matched.");
            }

            Result?.Set(context, result);
        }

        private static CdpParallelFindElementResult ParallelFindWithWndSelectors(string[] selectors, int timeoutMs)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(0, timeoutMs));
            do
            {
                for (var i = 0; i < selectors.Length; i++)
                {
                    try
                    {
                        var element = CdpElementLocator.FindBySelector(selectors[i], null, 0, 0, 0, false);
                        if (element != null)
                        {
                            return new CdpParallelFindElementResult(i, element);
                        }
                    }
                    catch
                    {
                    }
                }

                if (timeoutMs <= 0)
                {
                    break;
                }

                System.Threading.Thread.Sleep(10);
            }
            while (DateTime.UtcNow < deadline);

            return CdpParallelFindElementResult.NotFound();
        }
    }
}
