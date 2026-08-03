using System.Diagnostics;

namespace F2B.Browser.Chromium.Cdp.Browser
{
    /// <summary>
    /// Result of a successful browser open operation.
    /// </summary>
    public class BrowserOpenResult
    {
        public int Port { get; internal set; }

        public string ExecutablePath { get; internal set; }

        public string UserDataDir { get; internal set; }

        public string BrowserName { get; internal set; }

        public Process Process { get; internal set; }

        public bool AttachedToExisting { get; internal set; }
    }
}
