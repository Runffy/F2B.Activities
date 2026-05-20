using System.Threading;

namespace F2B.Browser.IExplore
{
    /// <summary>Shared helpers for IE workflow activities.</summary>
    public static class IeAutomation
    {
        public static void ApplyDelay(int delayMs)
        {
            if (delayMs > 0)
                Thread.Sleep(delayMs);
        }
    }
}
