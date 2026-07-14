using System;
using System.IO;

namespace F2B.Browser.Chromium.Cdp.Launcher
{
    internal static class UserDataPathBuilder
    {
        /// <summary>
        /// Builds {root}\BrowserData\{BrowserType}\{port}, e.g. C:\Temp\BrowserData\Chrome\9222.
        /// </summary>
        public static string BuildEffectivePath(string userDataDirRoot, string browserType, int port)
        {
            if (string.IsNullOrWhiteSpace(userDataDirRoot))
            {
                throw new ArgumentException("User data directory root is required.", "userDataDirRoot");
            }

            if (port <= 0 || port > 65535)
            {
                throw new ArgumentOutOfRangeException("port", "Port must be between 1 and 65535.");
            }

            var folderName = NormalizeBrowserFolderName(browserType);
            return Path.GetFullPath(Path.Combine(userDataDirRoot.Trim(), "BrowserData", folderName, port.ToString()));
        }

        public static string NormalizeBrowserFolderName(string browserType)
        {
            if (string.Equals(browserType, "MsEdge", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(browserType, "Edge", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(browserType, "msedge", StringComparison.OrdinalIgnoreCase))
            {
                return "MsEdge";
            }

            return "Chrome";
        }

        public static string ToExecutableHint(string browserType)
        {
            return string.Equals(NormalizeBrowserFolderName(browserType), "MsEdge", StringComparison.Ordinal)
                ? "edge"
                : "chrome";
        }
    }
}
