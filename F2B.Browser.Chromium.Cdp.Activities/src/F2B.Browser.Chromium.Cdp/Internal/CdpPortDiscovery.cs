using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal static class CdpPortDiscovery
    {
        private const string LocalHost = "127.0.0.1";
        private const string DevToolsActivePortFileName = "DevToolsActivePort";

        /// <summary>
        /// Discovers CDP debugging ports.
        /// When <paramref name="portFilter"/> is set, only that port is checked.
        /// Otherwise prefers Chrome/Edge command-line ports (explicit first, then
        /// <c>--remote-debugging-port=0</c> via DevToolsActivePort when
        /// <c>--user-data-dir</c> is also present), and only falls back to scanning
        /// local listening TCP ports when no command-line port is found.
        /// </summary>
        public static IList<int> DiscoverBrowserPorts(int? portFilter = null)
        {
            IList<int> candidates;
            if (portFilter.HasValue)
            {
                candidates = new List<int> { portFilter.Value };
            }
            else
            {
                candidates = ListPortsFromBrowserCommandLines();
                if (candidates.Count == 0)
                {
                    candidates = ListLocalListeningPorts();
                }
            }

            var result = new List<int>();
            foreach (var port in candidates.Distinct())
            {
                if (!IsSupportedBrowserPort(port))
                {
                    continue;
                }

                result.Add(port);
            }

            return result;
        }

        /// <summary>
        /// Reads debugging ports from chrome/msedge process command lines.
        /// <list type="bullet">
        /// <item>No <c>--remote-debugging-port</c> → ignored.</item>
        /// <item>Port &gt; 0 → tried first.</item>
        /// <item>Port == 0 → only if explicit <c>--user-data-dir</c> is present;
        /// resolved via that profile's <c>DevToolsActivePort</c> and tried last.
        /// Without <c>--user-data-dir</c>, ignored (no system-profile fallback).</item>
        /// </list>
        /// </summary>
        public static IList<int> ListPortsFromBrowserCommandLines()
        {
            var explicitPorts = new List<int>();
            var seenExplicitPorts = new HashSet<int>();
            var deferredUserDataDirs = new List<string>();
            var seenUserDataDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var processName in new[] { "chrome", "msedge" })
            {
                Process[] processes;
                try
                {
                    processes = Process.GetProcessesByName(processName);
                }
                catch
                {
                    continue;
                }

                foreach (var process in processes)
                {
                    try
                    {
                        var commandLine = ProcessCommandLine.GetCommandLine(process.Id);
                        if (string.IsNullOrWhiteSpace(commandLine))
                        {
                            continue;
                        }

                        var portText = ProcessCommandLine.ExtractArgumentValue(
                            commandLine, "--remote-debugging-port");
                        int port;
                        if (string.IsNullOrWhiteSpace(portText) ||
                            !int.TryParse(portText.Trim(), out port) ||
                            port < 0)
                        {
                            // No clear --remote-debugging-port → ignore.
                            continue;
                        }

                        if (port > 0)
                        {
                            if (seenExplicitPorts.Add(port))
                            {
                                explicitPorts.Add(port);
                            }

                            continue;
                        }

                        // port == 0: require explicit --user-data-dir; otherwise ignore.
                        string userDataDir;
                        if (!TryGetExplicitUserDataDir(commandLine, out userDataDir))
                        {
                            continue;
                        }

                        if (seenUserDataDirs.Add(userDataDir))
                        {
                            deferredUserDataDirs.Add(userDataDir);
                        }
                    }
                    catch
                    {
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }

            var ports = new List<int>(explicitPorts);
            var seenPorts = new HashSet<int>(explicitPorts);
            foreach (var userDataDir in deferredUserDataDirs)
            {
                int ephemeralPort;
                if (!TryReadDevToolsActivePort(userDataDir, out ephemeralPort) ||
                    ephemeralPort <= 0 ||
                    !seenPorts.Add(ephemeralPort))
                {
                    continue;
                }

                ports.Add(ephemeralPort);
            }

            return ports;
        }

        /// <summary>
        /// Reads the live CDP port from <c>{userDataDir}/DevToolsActivePort</c> (first line).
        /// </summary>
        public static bool TryReadDevToolsActivePort(string userDataDir, out int port)
        {
            port = 0;
            if (string.IsNullOrWhiteSpace(userDataDir))
            {
                return false;
            }

            try
            {
                var path = Path.Combine(userDataDir, DevToolsActivePortFileName);
                if (!File.Exists(path))
                {
                    return false;
                }

                string firstLine;
                using (var reader = new StreamReader(path))
                {
                    firstLine = reader.ReadLine();
                }

                if (string.IsNullOrWhiteSpace(firstLine))
                {
                    return false;
                }

                return int.TryParse(firstLine.Trim(), out port) && port > 0;
            }
            catch
            {
                port = 0;
                return false;
            }
        }

        /// <summary>
        /// Returns true only when the command line has an explicit <c>--user-data-dir</c>.
        /// Does not fall back to the system default profile.
        /// </summary>
        public static bool TryGetExplicitUserDataDir(string commandLine, out string userDataDir)
        {
            userDataDir = null;
            var fromArgs = ProcessCommandLine.ExtractArgumentValue(commandLine, "--user-data-dir");
            if (string.IsNullOrWhiteSpace(fromArgs))
            {
                return false;
            }

            userDataDir = NormalizePath(fromArgs.Trim().Trim('"'));
            return !string.IsNullOrWhiteSpace(userDataDir);
        }

        public static bool IsSupportedBrowserPort(int port)
        {
            if (!CdpConnectionChecker.CanConnect(LocalHost, port))
            {
                return false;
            }

            try
            {
                var version = CdpJsonClient.GetBrowserVersion(port);
                return BrowserNameHelper.IsSupportedBrowser(version.BrowserName);
            }
            catch
            {
                return false;
            }
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            try
            {
                return Path.GetFullPath(path)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
        }

        private static IList<int> ListLocalListeningPorts()
        {
            var ports = new HashSet<int>();
            var output = RunCommand("netstat", "-nao -p TCP");

            foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.IndexOf("LISTENING", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5)
                {
                    continue;
                }

                var localAddress = parts[1];
                if (!TryGetPortFromAddress(localAddress, out var localPort))
                {
                    continue;
                }

                if (!IsLocalHostAddress(localAddress))
                {
                    continue;
                }

                ports.Add(localPort);
            }

            return ports.ToList();
        }

        private static bool IsLocalHostAddress(string localAddress)
        {
            return localAddress.StartsWith("127.0.0.1:", StringComparison.OrdinalIgnoreCase)
                || localAddress.StartsWith("[::1]:", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetPortFromAddress(string address, out int port)
        {
            port = 0;
            if (string.IsNullOrWhiteSpace(address))
            {
                return false;
            }

            var colonIndex = address.LastIndexOf(':');
            if (colonIndex < 0 || colonIndex >= address.Length - 1)
            {
                return false;
            }

            return int.TryParse(address.Substring(colonIndex + 1), out port);
        }

        private static string RunCommand(string fileName, string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = Process.Start(startInfo))
            {
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(10000);
                return output;
            }
        }
    }
}
