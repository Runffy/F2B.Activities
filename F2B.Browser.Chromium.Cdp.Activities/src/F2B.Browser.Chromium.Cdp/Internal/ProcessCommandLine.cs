using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    /// <summary>
    /// Process / command-line helpers used by CDP browser discovery.
    /// </summary>
    internal static class ProcessCommandLine
    {
        public static string GetCommandLine(int pid)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    string.Format("SELECT CommandLine FROM Win32_Process WHERE ProcessId = {0}", pid)))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        return obj["CommandLine"] as string;
                    }
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        public static string ExtractArgumentValue(string commandLine, string argumentName)
        {
            if (string.IsNullOrEmpty(commandLine) || string.IsNullOrEmpty(argumentName))
            {
                return null;
            }

            var searchIndex = 0;
            while (searchIndex < commandLine.Length)
            {
                var index = commandLine.IndexOf(argumentName, searchIndex, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    return null;
                }

                var afterName = index + argumentName.Length;
                if (afterName >= commandLine.Length)
                {
                    return null;
                }

                if (commandLine[afterName] == '=')
                {
                    return ReadArgumentToken(commandLine, afterName + 1);
                }

                if (char.IsWhiteSpace(commandLine[afterName]))
                {
                    return ReadArgumentToken(commandLine, afterName + 1);
                }

                searchIndex = afterName;
            }

            return null;
        }

        public static IList<int> GetListeningProcessIds(int port)
        {
            var pids = new HashSet<int>();
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
                if (!TryGetPortFromAddress(localAddress, out var localPort) || localPort != port)
                {
                    continue;
                }

                int pid;
                if (int.TryParse(parts[parts.Length - 1], out pid) && pid > 0)
                {
                    pids.Add(pid);
                }
            }

            return pids.ToList();
        }

        public static int GetParentProcessId(int processId)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    string.Format("SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = {0}", processId)))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        return Convert.ToInt32(obj["ParentProcessId"]);
                    }
                }
            }
            catch
            {
                return 0;
            }

            return 0;
        }

        public static IEnumerable<int> GetChildProcessIds(int parentPid)
        {
            var childPids = new List<int>();

            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    string.Format("SELECT ProcessId FROM Win32_Process WHERE ParentProcessId = {0}", parentPid)))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        childPids.Add(Convert.ToInt32(obj["ProcessId"]));
                    }
                }
            }
            catch
            {
                // WMI may be unavailable.
            }

            return childPids;
        }

        private static string ReadArgumentToken(string commandLine, int startIndex)
        {
            while (startIndex < commandLine.Length && char.IsWhiteSpace(commandLine[startIndex]))
            {
                startIndex++;
            }

            if (startIndex >= commandLine.Length)
            {
                return null;
            }

            if (commandLine[startIndex] == '"')
            {
                var endQuote = commandLine.IndexOf('"', startIndex + 1);
                return endQuote < 0
                    ? commandLine.Substring(startIndex + 1)
                    : commandLine.Substring(startIndex + 1, endQuote - startIndex - 1);
            }

            var endIndex = startIndex;
            while (endIndex < commandLine.Length && !char.IsWhiteSpace(commandLine[endIndex]))
            {
                endIndex++;
            }

            return commandLine.Substring(startIndex, endIndex - startIndex);
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
