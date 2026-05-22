using System;
using System.Text;
using F2B.Browser.IExplore.Com;
using F2B.Browser.IExplore.Native;

namespace F2B.Browser.IExplore
{
    /// <summary>Debug helpers for Trident host / IE_Server layering (Edge IE Mode, embedded hosts).</summary>
    public static class TridentHostDiagnostics
    {
        private const string LogPrefix = "C#诊断 ";

        /// <summary>
        /// List all <c>Internet Explorer_Server</c> HWNDs under the connected top-level window,
        /// optionally probe <c>getElementById</c> on each document. Output goes to <see cref="Console.Out"/>.
        /// </summary>
        public static void DiagnoseTridentHost(EmbeddedIEWindow window, string probeElementId = null)
        {
            if (window == null)
                throw new ArgumentNullException(nameof(window));

            DiagnoseTridentHost(window.Handle, window.IeServerHandle, probeElementId);
        }

        /// <summary>Same as <see cref="DiagnoseTridentHost(EmbeddedIEWindow, string)"/> when only the top HWND is known.</summary>
        public static void DiagnoseTridentHost(IntPtr topLevelHwnd, string probeElementId = null)
        {
            DiagnoseTridentHost(topLevelHwnd, IntPtr.Zero, probeElementId);
        }

        /// <summary>
        /// Enumerate IE_Server layers and write URL/title/probe results.
        /// <paramref name="currentIeServer"/> is the HWND used by Find Window (0 = unknown).
        /// </summary>
        public static void DiagnoseTridentHost(IntPtr topLevelHwnd, IntPtr currentIeServer, string probeElementId = null)
        {
            if (topLevelHwnd == IntPtr.Zero)
            {
                Console.WriteLine(LogPrefix + "顶层窗句柄为空");
                return;
            }

            var probe = (probeElementId ?? string.Empty).Trim();
            Console.WriteLine(LogPrefix + "顶层窗 hwnd=0x" + topLevelHwnd.ToInt64().ToString("X")
                + " class=" + Win32Native.GetClassNameString(topLevelHwnd)
                + " title=" + Win32Native.GetWindowTextString(topLevelHwnd));

            if (currentIeServer != IntPtr.Zero)
                Console.WriteLine(LogPrefix + "Find Window 当前 IeServer=0x" + currentIeServer.ToInt64().ToString("X"));

            var servers = HtmlDocumentHelper.CollectInternetExplorerServers(topLevelHwnd);
            Console.WriteLine(LogPrefix + "Internet Explorer_Server 数量=" + servers.Count
                + " (去重后，每个 hwnd 只列一次)");

            if (servers.Count == 0)
            {
                DumpShallowChildTree(topLevelHwnd, maxDepth: 3);
                return;
            }

            for (var i = 0; i < servers.Count; i++)
            {
                var ie = servers[i];
                var marker = currentIeServer != IntPtr.Zero && ie == currentIeServer ? " <-- 当前绑定" : string.Empty;
                Console.WriteLine(LogPrefix + "[" + i + "] IeServer hwnd=0x" + ie.ToInt64().ToString("X") + marker);

                try
                {
                    var doc = HtmlDocumentHelper.GetDocumentFromIeServer(ie);
                    var url = HtmlDocumentHelper.ReadDocumentUrl(doc);
                    var title = HtmlDocumentHelper.ReadDocumentTitle(doc);
                    var ready = HtmlDocumentHelper.ReadReadyState(doc);
                    Console.WriteLine(LogPrefix + "[" + i + "]   url=" + url
                        + " title=" + title
                        + " readyState=" + ready);

                    if (probe.Length > 0)
                    {
                        var hit = ProbeElementById(doc, probe);
                        Console.WriteLine(LogPrefix + "[" + i + "]   getElementById('" + probe + "')="
                            + (hit ? "HIT" : "miss"));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(LogPrefix + "[" + i + "]   文档获取失败: " + ex.Message);
                }
            }
        }

        private static bool ProbeElementById(IHTMLDocument2 doc, string id)
        {
            try
            {
                dynamic d = doc;
                var el = d.getElementById(id);
                return el != null;
            }
            catch
            {
                return false;
            }
        }

        private static void DumpShallowChildTree(IntPtr root, int maxDepth)
        {
            Console.WriteLine(LogPrefix + "未找到 IE_Server，子窗口树 (depth<=" + maxDepth + "):");
            DumpChildTree(root, 0, maxDepth, new StringBuilder());
        }

        private static void DumpChildTree(IntPtr hwnd, int depth, int maxDepth, StringBuilder indent)
        {
            if (depth > maxDepth)
                return;

            var pad = indent.ToString();
            Console.WriteLine(LogPrefix + pad + "hwnd=0x" + hwnd.ToInt64().ToString("X")
                + " class=" + Win32Native.GetClassNameString(hwnd));

            if (depth >= maxDepth)
                return;

            Win32Native.EnumChildWindows(hwnd, (child, _) =>
            {
                indent.Append("  ");
                DumpChildTree(child, depth + 1, maxDepth, indent);
                if (indent.Length >= 2)
                    indent.Length -= 2;
                return true;
            }, IntPtr.Zero);
        }
    }
}
