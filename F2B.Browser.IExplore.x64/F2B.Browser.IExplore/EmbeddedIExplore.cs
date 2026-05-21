using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using F2B.Browser.IExplore.Com;
using F2B.Browser.IExplore.Native;

namespace F2B.Browser.IExplore
{
    /// <summary>
    /// Core entry for embedded IE automation (Win32 + MSHTML + SHDocVw).
    /// </summary>
    public static class EmbeddedIExplore
    {
        /// <summary>
        /// Enumerate all top-level windows that expose <c>Internet Explorer_Server</c> (for debugging Connect).
        /// </summary>
        public static IList<TridentWindowInfo> ListTridentWindows(bool includeHidden = false, bool browsersOnly = true)
        {
            var list = new List<TridentWindowInfo>();
            var seen = new HashSet<long>();

            void AddHwnd(IntPtr hwnd)
            {
                try
                {
                    if (hwnd == IntPtr.Zero || !seen.Add(hwnd.ToInt64()))
                        return;
                    if (!Win32Native.IsWindow(hwnd))
                        return;

                    var frame = IeHostWindow.ResolveBrowserFrameHwnd(hwnd);
                    if (frame != IntPtr.Zero)
                        hwnd = frame;
                    else if (browsersOnly)
                        return;

                    if (browsersOnly && !IeHostWindow.IsInternetExplorerBrowser(hwnd))
                        return;

                    if (!includeHidden && !Win32Native.IsWindowVisible(hwnd))
                        return;

                    var ieServer = HtmlDocumentHelper.FindInternetExplorerServer(hwnd);
                    if (ieServer == IntPtr.Zero)
                        return;

                    var shell = ShDocVwHelper.FindByHwnd((int)hwnd.ToInt64());
                    var url = shell?.LocationUrl ?? string.Empty;
                    var docTitle = shell?.Name ?? string.Empty;

                    IntPtr ieServerOut;
                    IHTMLDocument2 doc;
                    bool ok = HtmlDocumentHelper.TryGetDocument(hwnd, out ieServerOut, out doc);
                    if (ok)
                    {
                        if (string.IsNullOrEmpty(url))
                            url = HtmlDocumentHelper.ReadDocumentUrl(doc);
                        if (string.IsNullOrEmpty(docTitle))
                            docTitle = HtmlDocumentHelper.ReadDocumentTitle(doc);
                    }

                    list.Add(new TridentWindowInfo
                    {
                        Handle = hwnd,
                        ClassName = Win32Native.GetClassNameString(hwnd),
                        WindowTitle = Win32Native.GetWindowTextString(hwnd),
                        DocumentTitle = docTitle,
                        Url = url,
                        DocumentAccessible = ok || shell != null
                    });
                }
                catch (AccessViolationException) { /* skip */ }
            }

            Win32Native.EnumWindows((hwnd, _) =>
            {
                var cls = Win32Native.GetClassNameString(hwnd);
                if (cls.Equals(IeHostWindow.IeFrameClass, StringComparison.OrdinalIgnoreCase))
                    AddHwnd(hwnd);
                return true;
            }, IntPtr.Zero);

            foreach (var browser in ShDocVwHelper.EnumerateShellWindows(internetExplorerOnly: browsersOnly))
                AddHwnd(new IntPtr(browser.Hwnd));

            list.Sort((a, b) => a.Handle.ToInt64().CompareTo(b.Handle.ToInt64()));
            return list;
        }

        /// <summary>
        /// Find a top-level window that hosts Trident and matches all given criteria.
        /// </summary>
        /// <param name="criteria">
        /// Keys: <see cref="IEConnectCriteria.Title"/> (substring in window or document title),
        /// <see cref="IEConnectCriteria.TitleRegex"/>, <see cref="IEConnectCriteria.Url"/> (substring),
        /// <see cref="IEConnectCriteria.UrlRegex"/>, <see cref="IEConnectCriteria.ClassName"/> (exact).
        /// </param>
        /// <param name="index">Zero-based index when multiple windows match.</param>
        /// <param name="timeout">Maximum wait time in milliseconds (default <see cref="OperationDefaults.TimeoutMs"/>).</param>
        public static EmbeddedIEWindow Connect(
            IDictionary<string, string> criteria,
            int index = 0,
            int timeout = OperationDefaults.TimeoutMs)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            OperationTimeout.Validate(timeout, nameof(timeout));

            var rules = NormalizeCriteria(criteria);
            var sw = Stopwatch.StartNew();
            List<EmbeddedIEWindow> matches = null;

            while (sw.ElapsedMilliseconds < timeout)
            {
                matches = CollectMatches(rules);
                if (matches.Count > index)
                    return matches[index];

                Thread.Sleep(150);
            }

            if (matches.Count == 0)
            {
                throw new TimeoutException(
                    $"Timed out after {timeout} ms. " + BuildNoMatchMessage(rules));
            }

            throw new InvalidOperationException(
                $"Found {matches.Count} matching window(s), but index={index} is out of range.");
        }

        private static List<EmbeddedIEWindow> CollectMatches(CriteriaRules rules)
        {
            var matches = new List<EmbeddedIEWindow>();
            var seen = new HashSet<long>();

            void TryAdd(IntPtr hwnd, bool requireVisible = true)
            {
                try
                {
                    if (hwnd == IntPtr.Zero)
                        return;
                    if (!Win32Native.IsWindow(hwnd))
                        return;
                    if (requireVisible && !Win32Native.IsWindowVisible(hwnd))
                        return;

                    var topClass = Win32Native.GetClassNameString(hwnd);
                    if (!MatchClassName(rules, topClass))
                        return;

                    var connectHwnd = hwnd;
                    var frame = IeHostWindow.ResolveBrowserFrameHwnd(hwnd);
                    if (frame != IntPtr.Zero)
                        connectHwnd = frame;

                    if (!seen.Add(connectHwnd.ToInt64()))
                        return;

                    var ieServer = HtmlDocumentHelper.FindInternetExplorerServer(connectHwnd);
                    if (ieServer == IntPtr.Zero)
                        return;

                    var isStandaloneBrowser = IeHostWindow.IsInternetExplorerBrowser(connectHwnd);
                    if (!isStandaloneBrowser && string.IsNullOrEmpty(rules.ClassName)
                        && string.IsNullOrEmpty(rules.Title) && rules.TitleRegex == null
                        && string.IsNullOrEmpty(rules.Url) && rules.UrlRegex == null)
                        return;

                    var winTitle = Win32Native.GetWindowTextString(connectHwnd);
                    var shell = ShDocVwHelper.FindByHwnd((int)connectHwnd.ToInt64());
                    var docUrl = shell?.LocationUrl ?? string.Empty;
                    var docTitle = shell?.Name ?? string.Empty;

                    if (NeedsMsHtmlProbe(rules, docUrl, docTitle) || !isStandaloneBrowser)
                    {
                        IHTMLDocument2 doc;
                        if (HtmlDocumentHelper.TryGetDocument(connectHwnd, out _, out doc)
                            && HtmlDocumentHelper.IsDocumentReadable(doc))
                        {
                            if (string.IsNullOrEmpty(docUrl))
                                docUrl = HtmlDocumentHelper.ReadDocumentUrl(doc);
                            if (string.IsNullOrEmpty(docTitle))
                                docTitle = HtmlDocumentHelper.ReadDocumentTitle(doc);
                        }
                    }

                    if (!MatchTitle(rules, winTitle, docTitle))
                        return;
                    if (!MatchUrl(rules, docUrl))
                        return;

                    var hostClass = Win32Native.GetClassNameString(connectHwnd);
                    matches.Add(new EmbeddedIEWindow(connectHwnd, ieServer, hostClass));
                }
                catch (AccessViolationException) { /* skip */ }
            }

            foreach (var browser in ShDocVwHelper.EnumerateShellWindows(internetExplorerOnly: true))
            {
                try { TryAdd(new IntPtr(browser.Hwnd)); }
                catch { /* skip */ }
            }

            Win32Native.EnumWindows((hwnd, _) =>
            {
                try { TryAdd(hwnd); }
                catch { /* skip */ }
                return true;
            }, IntPtr.Zero);

            matches.Sort((a, b) => a.Handle.ToInt64().CompareTo(b.Handle.ToInt64()));
            return matches;
        }

        private sealed class CriteriaRules
        {
            public string Title;
            public Regex TitleRegex;
            public string Url;
            public Regex UrlRegex;
            public string ClassName;
        }

        private static CriteriaRules NormalizeCriteria(IDictionary<string, string> criteria)
        {
            var rules = new CriteriaRules();
            if (criteria == null || criteria.Count == 0)
                return rules;

            foreach (var kv in criteria)
            {
                if (string.IsNullOrWhiteSpace(kv.Key))
                    continue;

                var key = kv.Key.Trim();
                var value = kv.Value ?? string.Empty;

                if (key.Equals(IEConnectCriteria.Title, StringComparison.OrdinalIgnoreCase))
                    rules.Title = value;
                else if (key.Equals(IEConnectCriteria.TitleRegex, StringComparison.OrdinalIgnoreCase))
                    rules.TitleRegex = new Regex(value, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                else if (key.Equals(IEConnectCriteria.Url, StringComparison.OrdinalIgnoreCase))
                    rules.Url = value;
                else if (key.Equals(IEConnectCriteria.UrlRegex, StringComparison.OrdinalIgnoreCase))
                    rules.UrlRegex = new Regex(value, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                else if (key.Equals(IEConnectCriteria.ClassName, StringComparison.OrdinalIgnoreCase))
                    rules.ClassName = value;
                else
                    throw new ArgumentException("Unknown criteria key: " + key, nameof(criteria));
            }

            return rules;
        }

        private static bool MatchClassName(CriteriaRules rules, string className)
        {
            if (string.IsNullOrEmpty(rules.ClassName))
                return true;
            return string.Equals(className ?? string.Empty, rules.ClassName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// <paramref name="title"/> / <paramref name="documentTitle"/> — plain <c>title</c> matches if either contains the filter (IE often uses "Page - Internet Explorer").
        /// </summary>
        private static bool MatchTitle(CriteriaRules rules, string windowTitle, string documentTitle)
        {
            windowTitle = windowTitle ?? string.Empty;
            documentTitle = documentTitle ?? string.Empty;

            if (!string.IsNullOrEmpty(rules.Title))
            {
                bool inWindow = windowTitle.IndexOf(rules.Title, StringComparison.OrdinalIgnoreCase) >= 0;
                bool inDoc = documentTitle.IndexOf(rules.Title, StringComparison.OrdinalIgnoreCase) >= 0;
                if (!inWindow && !inDoc)
                    return false;
            }

            if (rules.TitleRegex != null)
            {
                bool winOk = rules.TitleRegex.IsMatch(windowTitle);
                bool docOk = rules.TitleRegex.IsMatch(documentTitle);
                if (!winOk && !docOk)
                    return false;
            }

            return true;
        }

        private static bool NeedsMsHtmlProbe(CriteriaRules rules, string docUrl, string docTitle)
        {
            var needsUrl = !string.IsNullOrEmpty(rules.Url) || rules.UrlRegex != null;
            var needsTitle = !string.IsNullOrEmpty(rules.Title) || rules.TitleRegex != null;

            if (needsUrl && string.IsNullOrEmpty(docUrl))
                return true;
            if (needsTitle && string.IsNullOrEmpty(docTitle))
                return true;
            return false;
        }

        private static bool MatchUrl(CriteriaRules rules, string url)
        {
            url = url ?? string.Empty;
            if (!string.IsNullOrEmpty(rules.Url) &&
                url.IndexOf(rules.Url, StringComparison.OrdinalIgnoreCase) < 0)
                return false;
            if (rules.UrlRegex != null && !rules.UrlRegex.IsMatch(url))
                return false;
            return true;
        }

        private static string BuildNoMatchMessage(CriteriaRules rules)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(rules.Title)) parts.Add("title contains \"" + rules.Title + "\"");
            if (rules.TitleRegex != null) parts.Add("title_regex=" + rules.TitleRegex);
            if (!string.IsNullOrEmpty(rules.Url)) parts.Add("url contains \"" + rules.Url + "\"");
            if (rules.UrlRegex != null) parts.Add("url_regex=" + rules.UrlRegex);
            if (!string.IsNullOrEmpty(rules.ClassName)) parts.Add("classname=" + rules.ClassName);

            var sb = new StringBuilder();
            if (parts.Count == 0)
                sb.Append("No window matched (provide at least one filter key).");
            else
                sb.Append("No window matched all criteria: ").Append(string.Join(", ", parts));

            sb.AppendLine();
            sb.AppendLine("Internet Explorer browser windows:");
            var browsers = ListTridentWindows(includeHidden: true, browsersOnly: true);
            if (browsers.Count == 0)
                sb.AppendLine("  (none — enable IE11, then run: F2B.Browser.IExplore.exe launch)");
            else
            {
                foreach (var w in browsers)
                    sb.AppendLine("  " + w);
            }

            var otherHosts = 0;
            foreach (var w in ListTridentWindows(includeHidden: true, browsersOnly: false))
            {
                if (!IeHostWindow.IsInternetExplorerBrowser(w.Handle))
                    otherHosts++;
            }
            if (otherHosts > 0)
                sb.AppendLine("Embedded/other Trident hosts (use Class Name or Url/Title on Find Window): " + otherHosts);

            return sb.ToString();
        }
    }
}
