using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Playwright
{
    public enum SendKeysBaseOn
    {
        Tab,
        Element
    }

    [DisplayName("Sendkeys")]
    [Description("Send keyboard keys to a tab or element.")]
    [Designer(typeof(CanvasFieldsActivityDesigner))]
    public sealed class SendkeysActivity : CodeActivity
    {
        [DisplayName("Base On")]
        [Description("Choose whether to send keys to a tab or element.")]
        [Category("Input")]
        [DefaultValue(SendKeysBaseOn.Tab)]
        public SendKeysBaseOn BaseOn { get; set; } = SendKeysBaseOn.Tab;

        [DisplayName("Input Tab")]
        [Description("Tab instance used to send keys.")]
        [Category("Input")]
        public InArgument<PwTab> InputTab { get; set; }

        [DisplayName("Selector")]
        [Description("Selector used when locating the target element.")]
        [Category("Input")]
        public InArgument<string> Selector { get; set; }

        [DisplayName("Target Type")]
        [Description("Specify whether to target by element or selector.")]
        [Category("Input")]
        [DefaultValue(ElementTargetType.Selector)]
        [TypeConverter("F2B.Browser.Chromium.Playwright.ElementTargetTypeConverter, F2B.Browser.Chromium.Playwright")]
        public ElementTargetType TargetType { get; set; } = ElementTargetType.Selector;

        [DisplayName("Input Element")]
        [Description("Element object that directly receives keys.")]
        [Category("Input")]
        public InArgument<object> InputElement { get; set; }

        [DisplayName("Delay Before")]
        [Description("Wait time in milliseconds before locating element.")]
        [Category("Time")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        [DisplayName("Timeout (ms)")]
        [Description("Total timeout in milliseconds for locate + send keys when BaseOn=Element.")]
        [Category("Time")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        [DisplayName("Keys")]
        [Description("Key content to send.")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<string> Keys { get; set; }

        [DisplayName("Delay")]
        [Description("Delay in milliseconds between key presses.")]
        [Category("Input")]
        public InArgument<int?> Delay { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var keys = Keys.Get(context);
            var delay = Delay == null ? null : Delay.Get(context);

            switch (BaseOn)
            {
                case SendKeysBaseOn.Tab:
                    var tab = InputTab == null ? null : InputTab.Get(context);
                    if (tab == null)
                    {
                        throw new InvalidOperationException("InputTab must be provided when BaseOn=Tab.");
                    }

                    tab.SendKeys(keys: keys, delay: delay);
                    break;
                default:
                    var totalMs = ActivityArgumentHelper.GetOrDefault(Timeout, context, 15000);
                    var budget = new TimeoutBudget(totalMs);
                    var element = ResolveElementTarget(context, budget.RemainingAsNullableDouble());
                    var remaining = budget.RemainingMs;
                    if (remaining <= 0)
                    {
                        throw new TimeoutException("Sendkeys timeout before operation: no remaining timeout budget after locating target.");
                    }

                    element.SendKeys(keys: keys, delay: delay, timeoutMs: remaining);
                    break;
            }
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
                            "InputElement must be provided when BaseOn=Element and TargetType=Element. Assign a variable or expression (for example [elm_parent]).");
                    }

                    throw new InvalidOperationException(
                        "InputElement must be provided when BaseOn=Element and TargetType=Element. The InputElement argument expression evaluated to null.");
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

            if (Keys == null || Keys.Expression == null)
            {
                metadata.AddValidationError("Keys is required.");
            }

            if (BaseOn == SendKeysBaseOn.Tab)
            {
                if (InputTab == null || InputTab.Expression == null)
                {
                    metadata.AddValidationError("InputTab must be provided when BaseOn=Tab.");
                }

                return;
            }
        }
    }
}
