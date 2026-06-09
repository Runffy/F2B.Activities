using System;
using System.Activities;
using System.Activities.Expressions;
using System.Threading;

namespace F2B.DesktopApplication.FlaUI
{
    internal static class ActivityArgumentHelper
    {
        internal static T GetOrDefault<T>(InArgument<T> argument, CodeActivityContext context, T defaultValue)
        {
            if (argument == null)
                return defaultValue;

            var value = argument.Get(context);
            return value == null ? defaultValue : value;
        }

        internal static void ApplyDelayBefore(InArgument<int> delayBefore, CodeActivityContext context, int defaultValue = 300)
        {
            var delayMs = GetOrDefault(delayBefore, context, defaultValue);
            if (delayMs <= 0)
                return;

            Thread.Sleep(delayMs);
        }

        internal static bool HasExpression<T>(InArgument<T> argument)
        {
            return argument != null && argument.Expression != null;
        }

        internal static string GetRequiredSelector(InArgument<string> selector, CodeActivityContext context)
        {
            var value = selector == null ? null : selector.Get(context);
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Selector XML is required.");

            return value;
        }

        internal static bool HasTextExpression(InArgument<string> argument)
        {
            if (argument == null || argument.Expression == null)
                return false;

            var literal = argument.Expression as Literal<string>;
            return literal == null || !string.IsNullOrWhiteSpace(literal.Value);
        }

        internal static void ValidateTextArgumentExpression(CodeActivityMetadata metadata, InArgument<string> argument, string message)
        {
            if (!HasTextExpression(argument))
                metadata.AddValidationError(message);
        }
    }
}
