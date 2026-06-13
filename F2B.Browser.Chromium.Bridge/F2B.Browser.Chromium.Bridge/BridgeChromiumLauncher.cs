using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

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

        /// <summary>
        /// Starts a new Chromium window via command line. Always uses --new-window so an existing
        /// browser session receives a new window instead of reusing a tab in the current window.
        /// </summary>
        public static void LaunchNewWindow(
            string chromeExecutablePath = null,
            string startUrl = null,
            string launchArguments = null,
            string extensionPath = null)
        {
            var chromePath = ResolveChromeExecutable(chromeExecutablePath);
            if (chromePath == null)
            {
                throw new InvalidOperationException(
                    "Chrome or Edge executable was not found. Set Chrome Executable Path on Open Browser.");
            }

            if (!string.IsNullOrWhiteSpace(extensionPath))
            {
                var normalized = Path.GetFullPath(extensionPath);
                if (!File.Exists(Path.Combine(normalized, "manifest.json")))
                {
                    throw new InvalidOperationException(
                        "Extension Path does not contain manifest.json: " + normalized);
                }
            }

            var arguments = BuildLaunchArguments(extensionPath, startUrl, launchArguments);
            Process.Start(new ProcessStartInfo
            {
                FileName = chromePath,
                Arguments = arguments,
                UseShellExecute = false
            });
        }

        private static string BuildLaunchArguments(
            string extensionPath,
            string startUrl,
            string launchArguments)
        {
            var args = new List<string>
            {
                "--no-first-run",
                "--no-default-browser-check",
                "--allow-file-access-from-files",
                "--new-window"
            };

            if (!string.IsNullOrWhiteSpace(launchArguments))
                args.Add(launchArguments.Trim());

            if (!string.IsNullOrWhiteSpace(extensionPath))
                args.Add(QuoteArgument("--load-extension=" + Path.GetFullPath(extensionPath)));

            if (!string.IsNullOrWhiteSpace(startUrl))
                args.Add(QuoteArgument(startUrl));

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
