using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace F2B.Browser.Chromium.Bridge
{
    internal static class BridgeChromiumLauncher
    {
        public static bool IsChromiumProcessRunning()
        {
            return Process.GetProcessesByName("chrome").Length > 0
                || Process.GetProcessesByName("msedge").Length > 0;
        }

        public static string ResolveChromeExecutable(string preferredPath = null)
        {
            if (!string.IsNullOrWhiteSpace(preferredPath) && File.Exists(preferredPath))
                return preferredPath;

            var candidates = new[]
            {
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "Microsoft", "Edge", "Application", "msedge.exe"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Microsoft", "Edge", "Application", "msedge.exe")
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        public static string ResolveExtensionPath(string preferredPath = null)
        {
            if (!string.IsNullOrWhiteSpace(preferredPath))
            {
                var normalized = Path.GetFullPath(preferredPath);
                if (File.Exists(Path.Combine(normalized, "manifest.json")))
                    return normalized;
            }

            var envPath = Environment.GetEnvironmentVariable("F2B_BRIDGE_EXTENSION_PATH");
            if (!string.IsNullOrWhiteSpace(envPath))
            {
                var normalized = Path.GetFullPath(envPath);
                if (File.Exists(Path.Combine(normalized, "manifest.json")))
                    return normalized;
            }

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var fromBaseDir = Path.Combine(baseDir, "extension");
            if (File.Exists(Path.Combine(fromBaseDir, "manifest.json")))
                return fromBaseDir;

            var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            for (var i = 0; i < 8 && !string.IsNullOrEmpty(assemblyDir); i++)
            {
                var candidate = Path.Combine(assemblyDir, "extension");
                if (File.Exists(Path.Combine(candidate, "manifest.json")))
                    return candidate;

                assemblyDir = Directory.GetParent(assemblyDir)?.FullName;
            }

            return null;
        }

        /// <summary>
        /// Starts the system Chromium profile when no browser process is running.
        /// Uses the default user-data-dir (same profile as manual Chrome usage).
        /// </summary>
        /// <returns>True when a new Chromium process was started.</returns>
        public static bool TryLaunch(
            string chromeExecutablePath = null,
            string extensionPath = null,
            string startUrl = null)
        {
            if (IsChromiumProcessRunning())
                return false;

            var chromePath = ResolveChromeExecutable(chromeExecutablePath);
            if (chromePath == null)
                return false;

            var arguments = BuildLaunchArguments(ResolveExtensionPath(extensionPath), startUrl);
            Process.Start(new ProcessStartInfo
            {
                FileName = chromePath,
                Arguments = arguments,
                UseShellExecute = false
            });

            return true;
        }

        private static string BuildLaunchArguments(string extensionPath, string startUrl)
        {
            var args = new List<string>
            {
                "--no-first-run",
                "--no-default-browser-check",
                "--allow-file-access-from-files"
            };

            if (!string.IsNullOrWhiteSpace(extensionPath))
                args.Add(QuoteArgument("--load-extension=" + extensionPath));

            if (!string.IsNullOrWhiteSpace(startUrl))
            {
                args.Add(QuoteArgument(startUrl));
            }
            else
            {
                args.Add("--no-startup-window");
            }

            return string.Join(" ", args);
        }

        private static string QuoteArgument(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "\"\"";

            if (value.IndexOf('"') >= 0 || value.IndexOf(' ') >= 0)
                return "\"" + value.Replace("\"", "\\\"") + "\"";

            return value;
        }
    }
}
