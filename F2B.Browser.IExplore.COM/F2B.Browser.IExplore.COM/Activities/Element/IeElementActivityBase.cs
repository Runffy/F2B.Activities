using System;
using System.Activities;
using System.Collections.Generic;
using System.ComponentModel;
using System.Activities.Expressions;

namespace F2B.Browser.IExplore.COM
{
    [DisplayName("IE Element Activity Base")]
    [Designer(typeof(RequiredFieldsActivityDesigner))]
    public abstract class IeElementActivityBase : CodeActivity
    {
        [DisplayName("Input Window")]
        [Category("Target")]
        [RequiredArgument]
        public InArgument<IEWindowController> InputWindow { get; set; }

        [DisplayName("Selector (Json String)")]
        [Category("Target")]
        [RequiredArgument]
        public InArgument<string> Selector { get; set; }

        [DisplayName("Frame Path (Json String)")]
        [Category("Target")]
        public InArgument<string> FramePath { get; set; }

        [DisplayName("Timeout")]
        [Category("Time")]
        [DefaultValue(15000)]
        public InArgument<int> Timeout { get; set; } = 15000;

        protected IEWindowController ResolveWindow(CodeActivityContext context)
        {
            var window = InputWindow == null ? null : InputWindow.Get(context);
            if (window == null)
                throw new ArgumentException("InputWindow is required.");

            return window;
        }

        protected IDictionary<string, object> ResolveSelector(CodeActivityContext context)
        {
            var selectorJson = Selector == null ? null : Selector.Get(context);
            return ActivityArgumentHelper.ParseJsonObject(selectorJson, "Selector", required: true);
        }

        protected IEnumerable<object> ResolveFramePath(CodeActivityContext context)
        {
            var framePathJson = FramePath == null ? null : FramePath.Get(context);
            return ActivityArgumentHelper.ParseJsonArray(framePathJson, "Frame Path", required: false);
        }

        protected int ResolveTimeout(CodeActivityContext context)
        {
            return ActivityArgumentHelper.GetOrDefault(Timeout, context, 15000);
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            base.CacheMetadata(metadata);

            ValidateArgumentExpression(metadata, InputWindow, "Input Window is required.");
            ValidateTextArgumentExpression(metadata, Selector, "Selector is required.");
        }

        protected static bool HasExpression<T>(InArgument<T> argument)
        {
            return argument != null && argument.Expression != null;
        }

        protected static bool HasTextExpression(InArgument<string> argument)
        {
            if (!HasExpression(argument))
            {
                return false;
            }

            var literal = argument.Expression as Literal<string>;
            if (literal == null)
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(literal.Value);
        }

        protected static void ValidateArgumentExpression<T>(CodeActivityMetadata metadata, InArgument<T> argument, string message)
        {
            if (!HasExpression(argument))
            {
                metadata.AddValidationError(message);
            }
        }

        protected static void ValidateTextArgumentExpression(CodeActivityMetadata metadata, InArgument<string> argument, string message)
        {
            if (!HasTextExpression(argument))
            {
                metadata.AddValidationError(message);
            }
        }
    }
}
