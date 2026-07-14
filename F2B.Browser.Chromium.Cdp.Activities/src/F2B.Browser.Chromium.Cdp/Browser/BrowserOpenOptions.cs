namespace F2B.Browser.Chromium.Cdp.Browser
{
    /// <summary>
    /// Options for opening a Chromium-based browser with CDP remote debugging.
    /// </summary>
    public class BrowserOpenOptions
    {
        /// <summary>
        /// CDP port. Values less than or equal to 0 mean auto port.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// When true, kill processes occupying the requested port or system profile before launch.
        /// </summary>
        public bool Force { get; set; }

        /// <summary>
        /// User data directory path, or special values: "system", "temp", "documents".
        /// When null or empty, defaults to "temp".
        /// </summary>
        public string UserDataDir { get; set; }

        /// <summary>
        /// Browser executable path, or special values: "chrome", "edge".
        /// When null or empty, defaults to "chrome".
        /// </summary>
        public string ExecutablePath { get; set; }

        /// <summary>
        /// Additional browser launch arguments as a single string.
        /// </summary>
        public string StartArguments { get; set; }
    }
}
