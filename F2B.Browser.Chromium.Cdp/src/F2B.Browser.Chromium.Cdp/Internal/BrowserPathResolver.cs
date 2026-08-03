using System;
using System.IO;
using F2B.Browser.Chromium.Cdp.Exceptions;
using Microsoft.Win32;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal static class BrowserPathResolver
    {
        public static string Resolve(string executablePath)
        {
            var input = string.IsNullOrWhiteSpace(executablePath) ? "chrome" : executablePath.Trim();

            if (IsSpecialBrowserName(input))
            {
                var resolved = ResolveFromRegistry(input);
                if (resolved == null)
                {
                    throw new BrowserException(
                        string.Format("Unable to locate {0} executable from registry.", input));
                }

                return Path.GetFullPath(resolved);
            }

            if (!File.Exists(input))
            {
                throw new BrowserException(
                    string.Format("Browser executable not found: {0}", input));
            }

            var fullPath = Path.GetFullPath(input);
            ValidateSupportedExecutable(fullPath);
            return fullPath;
        }

        public static bool IsSupportedExecutableFileName(string fileName)
        {
            return string.Equals(fileName, "chrome.exe", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "msedge.exe", StringComparison.OrdinalIgnoreCase);
        }

        private static void ValidateSupportedExecutable(string fullPath)
        {
            var fileName = Path.GetFileName(fullPath);
            if (!IsSupportedExecutableFileName(fileName))
            {
                throw new BrowserException(
                    string.Format(
                        "Unsupported browser executable: {0}. Only chrome.exe and msedge.exe are supported.",
                        string.IsNullOrEmpty(fileName) ? fullPath : fileName));
            }
        }

        public static string GetBrowserName(string executablePath)
        {
            var fileName = Path.GetFileName(executablePath);
            if (string.IsNullOrEmpty(fileName))
            {
                return "browser";
            }

            return Path.GetFileNameWithoutExtension(fileName);
        }

        public static bool IsChromeFamily(string browserName)
        {
            return string.Equals(browserName, "chrome", StringComparison.OrdinalIgnoreCase)
                || string.Equals(browserName, "msedge", StringComparison.OrdinalIgnoreCase)
                || string.Equals(browserName, "edge", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSpecialBrowserName(string value)
        {
            return string.Equals(value, "chrome", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "edge", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveFromRegistry(string browserName)
        {
            var exeName = string.Equals(browserName, "edge", StringComparison.OrdinalIgnoreCase)
                ? "msedge.exe"
                : "chrome.exe";

            var registryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + exeName;

            var path = ReadRegistryPath(Registry.CurrentUser, registryPath)
                ?? ReadRegistryPath(Registry.LocalMachine, registryPath);

            if (path != null && File.Exists(path))
            {
                return path;
            }

            return FindInPathEnvironment(exeName);
        }

        private static string ReadRegistryPath(RegistryKey root, string subKeyPath)
        {
            try
            {
                using (var key = root.OpenSubKey(subKeyPath, false))
                {
                    var value = key?.GetValue(null) as string;
                    return string.IsNullOrWhiteSpace(value) ? null : value.Trim('"');
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
                        return candidate;
                    }
                }
                catch
                {
                    // Ignore invalid PATH entries.
                }
            }

            return null;
        }
    }
}
