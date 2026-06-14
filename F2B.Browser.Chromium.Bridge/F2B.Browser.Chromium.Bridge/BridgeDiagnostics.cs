using System;

namespace F2B.Browser.Chromium.Bridge
{
    public static class BridgeDiagnostics
    {
        public static Action<string> Log { get; set; }

        internal static void Trace(string message)
        {
            try
            {
                BridgeFileLog.Write(message);
            }
            catch
            {
            }

            try
            {
                Log?.Invoke(message);
            }
            catch
            {
            }
        }
    }
}
