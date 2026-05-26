using System;
using System.Activities;
using System.Threading;

namespace F2B.Terminal.PCOMM
{
    internal static class PcommActivityHelper
    {
        internal static void ApplyDelayBefore(InArgument<int> delayBefore, CodeActivityContext context)
        {
            var delay = GetOrDefault(delayBefore, context, 300);
            if (delay > 0)
            {
                Thread.Sleep(delay);
            }
        }

        internal static int GetOrDefault(InArgument<int> argument, CodeActivityContext context, int fallback)
        {
            if (argument == null || argument.Expression == null)
            {
                return fallback;
            }

            return argument.Get(context);
        }
    }
}
