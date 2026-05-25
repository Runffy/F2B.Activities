using System;
using System.Activities;

namespace F2B.Browser.Chromium.Playwright
{
    internal static class ActivityArgumentHelper
    {
        public static T GetOrDefault<T>(InArgument<T> argument, CodeActivityContext context, T fallback)
        {
            if (argument == null || argument.Expression == null)
            {
                return fallback;
            }

            return argument.Get(context);
        }

        public static bool HasExpression(Argument argument)
        {
            return argument != null && argument.Expression != null;
        }

        public static PwElement GetPwElement(InArgument argument, CodeActivityContext context)
        {
            if (!HasExpression(argument))
            {
                return null;
            }

            return AsPwElement(argument.Get(context));
        }

        public static PwElement AsPwElement(object value)
        {
            if (value == null)
            {
                return null;
            }

            if (value is PwElement pwElement)
            {
                return pwElement;
            }

            throw new ArgumentException($"Expected {typeof(PwElement).FullName}, but received {value.GetType().FullName}.");
        }

        public static string ToWaitStateString(FindElementWaitState waitState)
        {
            switch (waitState)
            {
                case FindElementWaitState.None:
                    return null;
                case FindElementWaitState.Visible:
                    return "visible";
                case FindElementWaitState.Attached:
                    return "attached";
                case FindElementWaitState.Hidden:
                    return "hidden";
                case FindElementWaitState.Detached:
                    return "detached";
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(waitState), waitState, "Unsupported WaitState.");
            }
        }
    }
}
