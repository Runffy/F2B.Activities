using System;
using System.Collections.Generic;

namespace F2B.Browser.IExplore.Com
{
    /// <summary>Uses SHDocVw <c>Shell.Windows</c> (late-bound) to discover browser HWNDs and URLs.</summary>
    internal static class ShDocVwHelper
    {
        public sealed class BrowserEntry
        {
            public int Hwnd { get; set; }
            public string LocationUrl { get; set; }
            public string Name { get; set; }
            public string HostPath { get; set; }
            public bool IsInternetExplorer { get; set; }
        }

        public static IList<BrowserEntry> EnumerateShellWindows(bool internetExplorerOnly = false)
        {
            var list = new List<BrowserEntry>();
            try
            {
                var shellType = Type.GetTypeFromProgID("Shell.Windows");
                if (shellType == null)
                    return list;

                dynamic shellWindows = Activator.CreateInstance(shellType);
                if (shellWindows == null)
                    return list;

                int count = shellWindows.Count;
                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        dynamic browser = shellWindows.Item(i);
                        if (browser == null) continue;

                        int hwnd = 0;
                        try { hwnd = (int)browser.HWND; } catch { /* ignore */ }

                        string url = null;
                        try { url = (string)browser.LocationURL; } catch { /* ignore */ }

                        string name = null;
                        try { name = (string)browser.Name; } catch { /* ignore */ }

                        string hostPath = null;
                        try { hostPath = (string)browser.FullName; } catch { /* ignore */ }

                        var isIe = !string.IsNullOrEmpty(hostPath)
                            && hostPath.IndexOf("iexplore.exe", StringComparison.OrdinalIgnoreCase) >= 0;

                        if (internetExplorerOnly && !isIe)
                            continue;

                        if (hwnd != 0)
                        {
                            list.Add(new BrowserEntry
                            {
                                Hwnd = hwnd,
                                LocationUrl = url,
                                Name = name,
                                HostPath = hostPath,
                                IsInternetExplorer = isIe
                            });
                        }
                    }
                    catch
                    {
                        // skip one entry
                    }
                }
            }
            catch
            {
                // Shell.Windows not available
            }

            return list;
        }

        public static BrowserEntry FindByHwnd(int hwnd)
        {
            foreach (var e in EnumerateShellWindows())
            {
                if (e.Hwnd == hwnd)
                    return e;
            }
            return null;
        }
    }
}
