using System;
using System.Collections.Generic;
using System.Diagnostics;
using F2B.Browser.Chromium.Cdp.Browser;
using F2B.Browser.Chromium.Cdp.Exceptions;
using F2B.Browser.Chromium.Cdp.Internal;

namespace F2B.Browser.Chromium.Cdp
{
    /// <summary>
    /// Entry point for launching and attaching to Chromium-based browsers via CDP.
    /// </summary>
    public static class ChromiumBrowser
    {
        /// <summary>
        /// Opens a browser with CDP remote debugging enabled.
        /// </summary>
        public static CdpBrowser OpenBrowser(BrowserOpenOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            var executablePath = BrowserPathResolver.Resolve(options.ExecutablePath);
            var browserName = BrowserPathResolver.GetBrowserName(executablePath);
            var requestedPort = options.Port;
            var isAutoPort = requestedPort <= 0;
            var port = PortHelper.ResolvePort(requestedPort);

            var useSystemProfile = UserDataDirResolver.IsSystemProfile(options.UserDataDir);
            var userDataDir = UserDataDirResolver.Resolve(options.UserDataDir, browserName, port);

            if (useSystemProfile && options.Force)
            {
                var systemUserDataDir = UserDataDirResolver.ResolveSystemUserDataDir(browserName);
                ProcessHelper.KillProcessesUsingSystemProfile(executablePath, systemUserDataDir);
            }

            if (PortHelper.IsPortInUse(port))
            {
                if (CdpConnectionChecker.CanConnect(port))
                {
                    var existingUserDataDir = ProcessHelper.GetUserDataDirFromPort(port);
                    if (UserDataDirMatcher.IsSameUserDataDir(
                        options.UserDataDir, browserName, port, existingUserDataDir))
                    {
                        return CreateBrowser(executablePath, browserName, port, userDataDir, null, true);
                    }

                    if (!options.Force)
                    {
                        throw new BrowserException(
                            string.Format(
                                "Port {0} is occupied by a CDP browser with a different user_data_dir. " +
                                "Set Force=true to kill it and open a new browser.",
                                port));
                    }

                    ProcessHelper.KillProcessOnPort(port);
                    WaitUntilPortReleased(port);
                }
                else
                {
                    HandleNonCdpPortConflict(port, isAutoPort, options.Force);
                }
            }

            var process = LaunchBrowserProcess(
                executablePath, port, userDataDir, useSystemProfile, browserName, options.StartArguments);
            CdpConnectionChecker.WaitUntilReady(port);
            FirstRunDismisser.TryDismiss(port, browserName);

            return CreateBrowser(executablePath, browserName, port, userDataDir, process, false);
        }

        private static CdpBrowser CreateBrowser(
            string executablePath,
            string browserName,
            int port,
            string userDataDir,
            Process process,
            bool attachedToExisting)
        {
            var result = new BrowserOpenResult
            {
                Port = port,
                ExecutablePath = executablePath,
                UserDataDir = userDataDir,
                BrowserName = browserName,
                Process = process,
                AttachedToExisting = attachedToExisting
            };

            return CdpBrowser.FromOpenResult(result);
        }

        private static void HandleNonCdpPortConflict(int port, bool isAutoPort, bool force)
        {
            if (isAutoPort)
            {
                throw new BrowserException(
                    string.Format("Auto-selected port {0} is already in use.", port));
            }

            if (!force)
            {
                throw new BrowserException(
                    string.Format(
                        "Port {0} is already in use by a non-CDP process. Set Force=true to kill it.",
                        port));
            }

            ProcessHelper.KillProcessOnPort(port);
            WaitUntilPortReleased(port);
        }

        private static void WaitUntilPortReleased(int port)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline && PortHelper.IsPortInUse(port))
            {
                System.Threading.Thread.Sleep(200);
            }

            if (PortHelper.IsPortInUse(port))
            {
                throw new BrowserException(
                    string.Format("Port {0} is still in use after force kill.", port));
            }
        }

        private static Process LaunchBrowserProcess(
            string executablePath,
            int port,
            string userDataDir,
            bool useSystemProfile,
            string browserName,
            string startArguments)
        {
            var arguments = BuildLaunchArguments(port, userDataDir, useSystemProfile, browserName, startArguments);

            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                CreateNoWindow = false
            };

            try
            {
                return Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                throw new BrowserException(
                    string.Format("Failed to start browser: {0}", executablePath), ex);
            }
        }

        private static string BuildLaunchArguments(
            int port,
            string userDataDir,
            bool useSystemProfile,
            string browserName,
            string startArguments)
        {
            var args = new List<string>
            {
                string.Format("--remote-debugging-port={0}", port)
            };

            if (!useSystemProfile)
            {
                args.Add(string.Format("--user-data-dir=\"{0}\"", userDataDir));
            }

            var userArgs = CommandLineParser.FilterReservedArguments(
                CommandLineParser.ParseArguments(startArguments ?? string.Empty));

            var mergedArgs = DefaultLaunchArguments.MergeWithUserArguments(browserName, userArgs);
            args.AddRange(mergedArgs);

            return string.Join(" ", args);
        }
    }
}
