using System.Collections.Generic;

namespace F2B.Browser.Chromium.Cdp.Launcher
{
    public sealed class LauncherSettings
    {
        public string BrowserType { get; set; }

        public string ExecutablePath { get; set; }

        public int Port { get; set; }

        public string UserDataDirRoot { get; set; }

        public List<LaunchHistoryEntry> History { get; set; }

        public static LauncherSettings CreateDefault()
        {
            return new LauncherSettings
            {
                BrowserType = "Chrome",
                ExecutablePath = string.Empty,
                Port = 9222,
                UserDataDirRoot = @"C:\Temp",
                History = new List<LaunchHistoryEntry>()
            };
        }
    }
}
