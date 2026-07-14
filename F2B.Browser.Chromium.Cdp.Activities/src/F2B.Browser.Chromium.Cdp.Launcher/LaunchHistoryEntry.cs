using System;

namespace F2B.Browser.Chromium.Cdp.Launcher
{
    public sealed class LaunchHistoryEntry
    {
        public string BrowserType { get; set; }

        public string ExecutablePath { get; set; }

        public int Port { get; set; }

        public string UserDataDirRoot { get; set; }

        public string EffectiveUserDataDir { get; set; }

        public DateTime UsedAtUtc { get; set; }

        public string DisplayText
        {
            get
            {
                return string.Format(
                    "{0:yyyy-MM-dd HH:mm}  |  {1}  |  port {2}  |  {3}",
                    UsedAtUtc.ToLocalTime(),
                    BrowserType ?? "?",
                    Port,
                    EffectiveUserDataDir ?? UserDataDirRoot ?? "");
            }
        }
    }
}
