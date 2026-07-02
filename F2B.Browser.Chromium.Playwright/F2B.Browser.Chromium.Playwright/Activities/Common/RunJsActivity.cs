using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    public enum RunJsBaseOn
    {
        Element,
        Tab
    }

    [DisplayName("Run JavaScript")]
    [Description("Run JavaScript in a tab or element context.")]
    [Designer(typeof(CanvasFieldsActivityDesigner))]
    public sealed class RunJsActivity : CodeActivity
    {
        public RunJsActivity()
        {
            DisplayName = "Run JavaScript";
        }

        [DisplayName("Base On")]
        [Description("Choose whether to run script on a tab or element.")]
        [Category("Input.A")]
        [DefaultValue(RunJsBaseOn.Element)]
        public RunJsBaseOn BaseOn { get; set; } = RunJsBaseOn.Element;

        [DisplayName("Input Tab")]
        [Description("Tab instance used to run the script.")]
        [Category("Input.B")]
        public InArgument<PwTab> InputTab { get; set; }

        [DisplayName("Selector")]
        [Description("Selector used when locating the target element.")]
        [Category("Input.D")]
        public InArgument<string> Selector { get; set; }

        [DisplayName("Target Type")]
        [Description("Specify whether to target by element or selector.")]
        [Category("Input.C")]
        [DefaultValue(ElementTargetType.Selector)]
        [TypeConverter("F2B.Browser.Chromium.Playwright.ElementTargetTypeConverter, F2B.Browser.Chromium.Playwright")]
        public ElementTargetType TargetType { get; set; } = ElementTargetType.Selector;

        [DisplayName("Input Element")]
        [Description("Element object used directly as script target.")]
        [Category("Input.E")]
        public InArgument<PwElement> InputElement { get; set; }

        [DisplayName("Delay Before (ms)")]
        [Description("Wait time in milliseconds before locating element.")]
        [Category("Input.Z")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        [DisplayName("Timeout (ms)")]
        [Description("Total timeout in milliseconds for locate + script evaluation when BaseOn=Element.")]
        [Category("Input.Z")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        [DisplayName("Script")]
        [Description("JavaScript code to execute.")]
        [RequiredArgument]
        [Category("Input.F")]
        public InArgument<string> Script { get; set; }

        [DisplayName("Arg")]
        [Description("Argument object passed to the script.")]
        [Category("Input.G")]
        public InArgument<object> Arg { get; set; }

        [DisplayName("Is Async")]
        [Description("When true, starts script execution without waiting for the result.")]
        [Category("Input.G")]
        [DefaultValue(false)]
        public InArgument<bool> IsAsync { get; set; } = false;

        [DisplayName("Result")]
        [Description("Outputs the script execution result.")]
        [Category("Output")]
        public OutArgument<object> Result { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var script = Script.Get(context);
            var arg = Arg == null ? null : Arg.Get(context);
            var isAsync = ActivityArgumentHelper.GetOrDefault(IsAsync, context, false);
            object result;

            switch (BaseOn)
            {
                case RunJsBaseOn.Tab:
                    var tab = InputTab == null ? null : InputTab.Get(context);
                    if (tab == null)
                    {
                        throw new InvalidOperationException("InputTab must be provided when BaseOn=Tab.");
                    }

                    var tabTotalMs = ActivityArgumentHelper.GetOrDefault(Timeout, context, 15000);
                    var tabBudget = new TimeoutBudget(tabTotalMs);
                    PlaywrightSyncClient.ApplyDelay(ActivityArgumentHelper.GetOrDefault(DelayBefore, context, 300));
                    if (tabBudget.RemainingMs <= 0)
                    {
                        throw new TimeoutException("RunJs timeout before execution.");
                    }

                    result = tab.RunJs<object>(script, arg, tabBudget.RemainingMs, isAsync);
                    break;
                default:
                    var totalMs = ActivityArgumentHelper.GetOrDefault(Timeout, context, 15000);
                    var budget = new TimeoutBudget(totalMs);
                    var element = ResolveElementTarget(context, budget.RemainingAsNullableDouble());
                    var remaining = budget.RemainingMs;
                    if (remaining <= 0)
                    {
                        throw new TimeoutException("RunJs timeout before execution: no remaining timeout budget after locating target.");
                    }

                    result = element.RunJs<object>(script, arg, remaining, isAsync);
                    break;
            }

            Result?.Set(context, result);
        }

        private PwElement ResolveElementTarget(CodeActivityContext context, double? locateTimeout)
        {
            var delayBefore = ActivityArgumentHelper.GetOrDefault(DelayBefore, context, 300);

            if (TargetType == ElementTargetType.Element)
            {
                var element = ActivityArgumentHelper.GetPwElement(InputElement, context);
                if (element == null)
                {
                    if (!ActivityArgumentHelper.HasExpression(InputElement))
                    {
                        throw new InvalidOperationException(
                            "InputElement must be provided when BaseOn=Element and TargetType=Element. Assign a PwElement variable (for example elm_parent).");
                    }

                    throw new InvalidOperationException(
                        "InputElement must be provided when BaseOn=Element and TargetType=Element. Variable '" +
                        ActivityArgumentHelper.TryGetBoundVariableName(InputElement) +
                        "' is null.");
                }

                PlaywrightSyncClient.ApplyDelay(delayBefore);
                return element;
            }

            var selector = Selector == null ? null : Selector.Get(context);
            if (string.IsNullOrWhiteSpace(selector))
            {
                throw new InvalidOperationException("Selector must be provided when BaseOn=Element and TargetType=Selector.");
            }

            var tab = InputTab == null ? null : InputTab.Get(context);
            if (tab == null)
            {
                throw new InvalidOperationException("InputTab must be provided when BaseOn=Element and TargetType=Selector.");
            }

            var effectiveTimeout = locateTimeout.HasValue ? Math.Max(0, locateTimeout.Value) : (double?)null;
            var waitState = effectiveTimeout.HasValue ? "attached" : null;
            return tab.FindElement(selector, index: 0, timeout: effectiveTimeout, waitState: waitState, delayBefore: delayBefore);
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);

            if (BaseOn == RunJsBaseOn.Tab)
            {
                if (InputTab == null || InputTab.Expression == null)
                {
                    metadata.AddValidationError("InputTab must be provided when BaseOn=Tab.");
                }

                if (Script == null || Script.Expression == null)
                {
                    metadata.AddValidationError("Script is required.");
                }

                return;
            }

            if (Script == null || Script.Expression == null)
            {
                metadata.AddValidationError("Script is required.");
            }
        }
    }
}
