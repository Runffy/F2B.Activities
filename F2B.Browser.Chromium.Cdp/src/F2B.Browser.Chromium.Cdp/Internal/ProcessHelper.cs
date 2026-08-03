using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using F2B.Browser.Chromium.Cdp.Exceptions;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal static class ProcessHelper
    {
        public static void KillProcessOnPort(int port)
        {
            var pids = GetListeningProcessIds(port);
            if (pids.Count == 0)
            {
                return;
            }

            foreach (var pid in pids)
            {
                KillProcessTree(pid);
            }
        }

        /// <summary>
        /// Returns the user data dir of the process listening on the given port.
        /// Null means the process uses the default system profile (no --user-data-dir).
        /// </summary>
        public static string GetUserDataDirFromPort(int port)
        {
            foreach (var pid in GetListeningProcessIds(port))
            {
                var commandLine = GetCommandLine(pid);
                if (string.IsNullOrWhiteSpace(commandLine))
                {
                    continue;
                }

                var userDataDir = ExtractArgumentValue(commandLine, "--user-data-dir");
                if (string.IsNullOrWhiteSpace(userDataDir))
                {
                    // Command line available but no explicit user-data-dir → system profile.
                    return null;
                }

                return NormalizePath(userDataDir);
            }

            // Could not read command line from any listening process.
            return null;
        }

        public static void KillProcessesUsingSystemProfile(string executablePath, string systemUserDataDir)
        {
            var processName = Path.GetFileNameWithoutExtension(executablePath);
            var normalizedSystemDir = NormalizePath(systemUserDataDir);

            foreach (var process in Process.GetProcessesByName(processName))
            {
                try
                {
                    if (!IsUsingSystemProfile(process, normalizedSystemDir))
                    {
                        continue;
                    }

                    KillProcessTree(process.Id);
                }
                catch
                {
                    // Ignore processes we cannot inspect or terminate.
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        public static bool IsUsingSystemProfile(Process process, string normalizedSystemUserDataDir)
        {
            var commandLine = GetCommandLine(process.Id);
            if (string.IsNullOrWhiteSpace(commandLine))
            {
                // No explicit user-data-dir means default system profile.
                return true;
            }

            var userDataDir = ExtractArgumentValue(commandLine, "--user-data-dir");
            if (string.IsNullOrWhiteSpace(userDataDir))
            {
                return true;
            }

            return string.Equals(
                NormalizePath(userDataDir.Trim('"').Trim()),
                normalizedSystemUserDataDir,
                StringComparison.OrdinalIgnoreCase);
        }

        private static List<int> GetListeningProcessIds(int port)
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

        private static void KillProcessTree(int pid)
        {
            try
            {
                var process = Process.GetProcessById(pid);
                try
                {
                    foreach (var child in GetChildProcessIds(pid))
                    {
                        KillProcessTree(child);
                    }
                }
                catch
                {
                    // Continue even if child enumeration fails.
                }

                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(5000);
                }
            }
            catch (ArgumentException)
            {
                // Process already exited.
            }
            catch (Exception ex)
            {
                throw new BrowserException(
                    string.Format("Unable to kill process {0}.", pid), ex);
            }
        }

        private static IEnumerable<int> GetChildProcessIds(int parentPid)
        {
            var childPids = new List<int>();

            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    string.Format("SELECT ProcessId FROM Win32_Process WHERE ParentProcessId = {0}", parentPid)))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var childPid = Convert.ToInt32(obj["ProcessId"]);
                        childPids.Add(childPid);
                    }
                }
            }
            catch
            {
                // WMI may be unavailable; caller will still attempt to kill parent.
            }

            return childPids;
        }

        private static string GetCommandLine(int pid)
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

        private static string ExtractArgumentValue(string commandLine, string argumentName)
        {
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

        private static string NormalizePath(string path)
        {
            try
            {
                return Path.GetFullPath(path.Trim('"').Trim())
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return path.Trim('"').Trim()
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
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
