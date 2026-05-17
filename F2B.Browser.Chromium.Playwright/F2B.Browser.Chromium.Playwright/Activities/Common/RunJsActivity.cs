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
        [DisplayName("Base On")]
        [Description("Choose whether to run script on a tab or element.")]
        [Category("Input")]
        [DefaultValue(RunJsBaseOn.Element)]
        public RunJsBaseOn BaseOn { get; set; } = RunJsBaseOn.Element;

        [DisplayName("Input Tab")]
        [Description("Tab instance used to run the script.")]
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
        [Description("Element object used directly as script target.")]
        [Category("Input")]
        public InArgument<PwElement> InputElement { get; set; }

        [DisplayName("Delay Before")]
        [Description("Wait time in milliseconds before locating element.")]
        [Category("Time")]
        [DefaultValue(300)]
        public InArgument<int> DelayBefore { get; set; } = 300;

        [DisplayName("Script")]
        [Description("JavaScript code to execute.")]
        [RequiredArgument]
        [Category("Input")]
        public InArgument<string> Script { get; set; }

        [DisplayName("Arg")]
        [Description("Argument object passed to the script.")]
        [Category("Input")]
        public InArgument<object> Arg { get; set; }

        [DisplayName("Result")]
        [Description("Outputs the script execution result.")]
        [Category("Output")]
        public OutArgument<object> Result { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            var script = Script.Get(context);
            var arg = Arg == null ? null : Arg.Get(context);
            object result;

            switch (BaseOn)
            {
                case RunJsBaseOn.Tab:
                    var tab = InputTab == null ? null : InputTab.Get(context);
                    if (tab == null)
                    {
                        throw new InvalidOperationException("InputTab must be provided when BaseOn=Tab.");
                    }

                    result = tab.RunJs<object>(script, arg);
                    break;
                default:
                    var element = ResolveElementTarget(context);
                    result = element.RunJs<object>(script, arg);
                    break;
            }

            Result?.Set(context, result);
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

            if (Script == null || Script.Expression == null)
            {
                metadata.AddValidationError("Script is required.");
            }

            if (BaseOn == RunJsBaseOn.Tab)
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
