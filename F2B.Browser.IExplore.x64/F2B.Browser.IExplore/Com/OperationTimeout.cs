using System;
using System.Diagnostics;
using System.Threading;

namespace F2B.Browser.IExplore.Com
{
    internal static class OperationTimeout
    {
        public static void Validate(int timeoutMs, string paramName)
        {
            if (timeoutMs < 0)
                throw new ArgumentOutOfRangeException(paramName, "Timeout must be zero or positive.");
        }

        public static T WaitUntil<T>(int timeoutMs, Func<T> tryGet, Func<TimeoutException> onTimeout)
            where T : class
        {
            Validate(timeoutMs, "timeout");
            var sw = Stopwatch.StartNew();
            while (true)
            {
                var result = tryGet();
                if (result != null)
                    return result;

                if (sw.ElapsedMilliseconds >= timeoutMs)
                    throw onTimeout();

                Thread.Sleep(150);
            }
        }
    }
}
