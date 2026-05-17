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
