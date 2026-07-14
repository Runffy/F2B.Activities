using System;
using System.IO;
using Microsoft.Win32;

namespace F2B.Browser.Chromium.Cdp.Launcher
{
    internal static class BrowserExecutableLocator
    {
        public static string TryResolve(string browserType)
        {
            var exeName = string.Equals(
                    UserDataPathBuilder.NormalizeBrowserFolderName(browserType),
                    "MsEdge",
                    StringComparison.Ordinal)
                ? "msedge.exe"
                : "chrome.exe";

            var registryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + exeName;
            var path = ReadRegistryPath(Registry.CurrentUser, registryPath)
                ?? ReadRegistryPath(Registry.LocalMachine, registryPath);

            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                return Path.GetFullPath(path);
            }

            return FindInPathEnvironment(exeName);
        }

        private static string ReadRegistryPath(RegistryKey root, string subKeyPath)
        {
            try
            {
                using (var key = root.OpenSubKey(subKeyPath, false))
                {
                    var value = key == null ? null : key.GetValue(null) as string;
                    return string.IsNullOrWhiteSpace(value) ? null : value.Trim().Trim('"');
                }
            }
            catch
            {
                return null;
            }
        }

        private static string FindInPathEnvironment(string exeName)
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var folder in pathEnv.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    var candidate = Path.Combine(folder.Trim(), exeName);
                    if (File.Exists(candidate))
                    {
                        return Path.GetFullPath(candidate);
                    }
                }
                catch
                {
                }
            }

            return null;
        }
    }
}