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

        public static IList<int> DiscoverBrowserPorts(int? portFilter = null)
        {
            var ports = portFilter.HasValue
                ? new List<int> { portFilter.Value }
                : ListLocalListeningPorts();

            var result = new List<int>();
            foreach (var port in ports.Distinct().OrderBy(value => value))
            {
                if (!IsSupportedBrowserPort(port))
                {
                    continue;
                }

                result.Add(port);
            }

            return result;
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
