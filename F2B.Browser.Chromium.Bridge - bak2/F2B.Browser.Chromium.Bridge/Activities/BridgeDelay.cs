using System.Threading;

namespace F2B.Browser.Chromium.Bridge
{
    internal static class BridgeDelay
    {
        public static void Apply(int delayMs)
        {
            if (delayMs > 0)
                Thread.Sleep(delayMs);
        }
    }
}
