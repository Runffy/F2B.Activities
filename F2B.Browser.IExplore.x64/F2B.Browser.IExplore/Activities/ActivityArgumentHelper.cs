using System.Activities;

namespace F2B.Browser.IExplore
{
    internal static class ActivityArgumentHelper
    {
        public static T GetOrDefault<T>(InArgument<T> argument, CodeActivityContext context, T fallback)
        {
            if (argument == null || argument.Expression == null)
                return fallback;

            return argument.Get(context);
        }
    }
}
