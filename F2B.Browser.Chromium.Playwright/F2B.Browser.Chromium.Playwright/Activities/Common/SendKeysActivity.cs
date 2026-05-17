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

    [DisplayName("Send Target Keys")]
    [Description("Send keyboard keys to a tab or element.")]
    [Designer(typeof(CanvasFieldsActivityDesigner))]
    public sealed class SendKeysActivity : CodeActivity
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
        public InArgument<PwElement> InputElement { get; set; }

        [DisplayName("Delay Before")]
        [Description("Wait time in milliseconds before locating element.")]
        [Category("Time")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

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
                    var element = ResolveElementTarget(context);
                    element.SendKeys(keys: keys, delay: delay);
                    break;
            }
        }

        private PwElement ResolveElementTarget(CodeActivityContext context)
        {
            var delayBefore = ActivityArgumentHelper.GetOrDefault(DelayBefore, context, 300);

            if (TargetType == ElementTargetType.Element)
            {
                var element = InputElement == null ? null : InputElement.Get(context);
                if (element == null)
                {
                    throw new InvalidOperationException("InputElement must be provided when BaseOn=Element and TargetType=Element.");
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

            return tab.FindElement(selector, index: 0, timeout: null, waitState: null, delayBefore: delayBefore);
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

            if (TargetType == ElementTargetType.Element)
            {
                if (InputElement == null || InputElement.Expression == null)
                {
                    metadata.AddValidationError("InputElement must be provided when BaseOn=Element and TargetType=Element.");
                }
            }
            else if (TargetType == ElementTargetType.Selector)
            {
                if (InputTab == null || InputTab.Expression == null)
                {
                    metadata.AddValidationError("InputTab must be provided when BaseOn=Element and TargetType=Selector.");
                }

                if (Selector == null || Selector.Expression == null)
                {
                    metadata.AddValidationError("Selector must be provided when BaseOn=Element and TargetType=Selector.");
                }
            }
            else
            {
                metadata.AddValidationError("Unsupported TargetType.");
            }
        }
    }
}
