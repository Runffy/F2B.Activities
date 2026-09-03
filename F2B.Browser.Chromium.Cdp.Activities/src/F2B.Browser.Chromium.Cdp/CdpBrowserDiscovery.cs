using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using F2B.Browser.Chromium.Cdp.Internal;

namespace F2B.Browser.Chromium.Cdp
{
    /// <summary>
    /// Read-only discovery of local Chrome/Edge instances that expose a CDP debugging port.
    /// Used by CDP Inspector; does not launch or kill browsers.
    /// </summary>
    public static class CdpBrowserDiscovery
    {
        private static readonly object CacheSync = new object();
        private static readonly Dictionary<int, CacheEntry> ProcessCache = new Dictionary<int, CacheEntry>();
        private const int ProcessCacheTtlMs = 15000;

        private sealed class CacheEntry
        {
            public DateTime Utc;
            public CdpDiscoveredBrowser Browser;
            public bool ResolvedWithoutPort;
        }

        /// <summary>
        /// Lists CDP browsers by reading Chrome/Edge process command lines
        /// (<c>--remote-debugging-port</c>). Much faster than scanning every local TCP port.
        /// </summary>
        public static IList<CdpDiscoveredBrowser> ListDebuggingBrowsersFromProcesses()
        {
            var results = new List<CdpDiscoveredBrowser>();
            foreach (var port in CdpPortDiscovery.ListPortsFromBrowserCommandLines())
            {
                CdpDiscoveredBrowser browser;
                if (TryDescribePort(port, out browser))
                {
                    results.Add(browser);
                }
            }

            return results;
        }

        /// <summary>
        /// Lists every local listening port that speaks CDP for Chrome or Edge.
        /// Prefer <see cref="ListDebuggingBrowsersFromProcesses"/> for UI hot paths.
        /// </summary>
        public static IList<CdpDiscoveredBrowser> ListDebuggingBrowsers()
        {
            var fromProcesses = ListDebuggingBrowsersFromProcesses();
            if (fromProcesses.Count > 0)
            {
                return fromProcesses;
            }

            var results = new List<CdpDiscoveredBrowser>();
            foreach (var port in CdpPortDiscovery.DiscoverBrowserPorts())
            {
                if (TryDescribePort(port, out var browser))
                {
                    results.Add(browser);
                }
            }

            return results;
        }

        /// <summary>
        /// Fast process→port resolve for Indicate hover. Prefers command-line
        /// <c>--remote-debugging-port</c> on the process parent chain; caches results.
        /// Avoids netstat scans on the hot path.
        /// </summary>
        public static bool TryResolveFromProcessId(int processId, out CdpDiscoveredBrowser browser)
        {
            browser = null;
            if (processId <= 0)
            {
                return false;
            }

            lock (CacheSync)
            {
                CacheEntry cached;
                if (ProcessCache.TryGetValue(processId, out cached) &&
                    (DateTime.UtcNow - cached.Utc).TotalMilliseconds < ProcessCacheTtlMs)
                {
                    if (cached.Browser != null)
                    {
                        browser = cached.Browser;
                        return true;
                    }

                    if (cached.ResolvedWithoutPort)
                    {
                        return false;
                    }
                }
            }

            CdpDiscoveredBrowser resolved = null;
            var found = TryResolveFromProcessIdCore(processId, out resolved);

            lock (CacheSync)
            {
                ProcessCache[processId] = new CacheEntry
                {
                    Utc = DateTime.UtcNow,
                    Browser = resolved,
                    ResolvedWithoutPort = !found
                };
            }

            browser = resolved;
            return found;
        }

        /// <summary>
        /// Returns true when the process appears to be Chrome/Edge but has no usable CDP port.
        /// </summary>
        public static bool IsChromiumBrowserProcess(int processId)
        {
            if (processId <= 0)
            {
                return false;
            }

            try
            {
                using (var process = Process.GetProcessById(processId))
                {
                    var name = process.ProcessName ?? string.Empty;
                    return name.Equals("chrome", StringComparison.OrdinalIgnoreCase)
                        || name.Equals("msedge", StringComparison.OrdinalIgnoreCase)
                        || name.Equals("msedgewebview2", StringComparison.OrdinalIgnoreCase)
                        || name.IndexOf("chromium", StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool TryResolveFromProcessIdCore(int processId, out CdpDiscoveredBrowser browser)
        {
            browser = null;

            // Parent chain only (renderer → browser). Avoid enumerating all children on hover.
            var chain = new List<int>();
            var current = processId;
            for (var depth = 0; depth < 8 && current > 0; depth++)
            {
                chain.Add(current);
                var parent = ProcessCommandLine.GetParentProcessId(current);
                if (parent <= 0 || chain.Contains(parent))
                {
                    break;
                }

                current = parent;
            }

            foreach (var pid in chain)
            {
                var commandLine = ProcessCommandLine.GetCommandLine(pid);
                if (string.IsNullOrWhiteSpace(commandLine))
                {
                    continue;
                }

                var portText = ProcessCommandLine.ExtractArgumentValue(commandLine, "--remote-debugging-port");
                if (string.IsNullOrWhiteSpace(portText))
                {
                    // No clear --remote-debugging-port → ignore.
                    continue;
                }

                int port;
                if (!int.TryParse(portText.Trim(), out port) || port < 0)
                {
                    continue;
                }

                if (port == 0)
                {
                    // Ephemeral port: require explicit --user-data-dir (no system-profile fallback).
                    string userDataDir;
                    if (!CdpPortDiscovery.TryGetExplicitUserDataDir(commandLine, out userDataDir) ||
                        !CdpPortDiscovery.TryReadDevToolsActivePort(userDataDir, out port) ||
                        port <= 0)
                    {
                        continue;
                    }
                }

                if (TryDescribePort(port, out browser))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryDescribePort(int port, out CdpDiscoveredBrowser browser)
        {
            browser = null;
            try
            {
                if (!CdpPortDiscovery.IsSupportedBrowserPort(port))
                {
                    return false;
                }

                var version = CdpJsonClient.GetBrowserVersion(port);
                var browserName = BrowserNameHelper.InferBrowserName(version.Browser, version.UserAgent);
                if (!BrowserNameHelper.IsSupportedBrowser(browserName))
                {
                    browserName = version.BrowserName;
                }

                if (!BrowserNameHelper.IsSupportedBrowser(browserName))
                {
                    return false;
                }

                // Skip netstat for hot path — pid/userDataDir are optional metadata.
                browser = new CdpDiscoveredBrowser(
                    port,
                    browserName,
                    null,
                    0,
                    Enumerable.Empty<int>());
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Snapshot of a CDP-enabled browser discovered on the local machine.
    /// </summary>
    public sealed class CdpDiscoveredBrowser
    {
        public CdpDiscoveredBrowser(
            int port,
            string browserName,
            string userDataDir,
            int processId,
            IEnumerable<int> relatedProcessIds)
        {
            Port = port;
            BrowserName = browserName ?? string.Empty;
            UserDataDir = userDataDir;
            ProcessId = processId;
            RelatedProcessIds = new HashSet<int>(relatedProcessIds ?? Enumerable.Empty<int>());
            if (processId > 0)
            {
                RelatedProcessIds.Add(processId);
            }
        }

        public int Port { get; }

        /// <summary>Normalized name: chrome or msedge.</summary>
        public string BrowserName { get; }

        /// <summary>Null means system default profile (no explicit --user-data-dir).</summary>
        public string UserDataDir { get; }

        public int ProcessId { get; }

        public HashSet<int> RelatedProcessIds { get; }
    }
}
