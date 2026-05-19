using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading;
using Microsoft.Playwright;


namespace F2B.Browser.Chromium.Playwright
{
    public enum InputMethod
    {
        Fill,
        Type
    }

    public enum ClickValidateMode
    {
        None,
        ElementDisappear,
        ElementAppear
    }

    public enum FindElementWaitState
    {
        None,
        Visible,
        Attached,
        Hidden,
        Detached
    }

    public sealed class PlaywrightSyncClient : IDisposable
    {
        private IPlaywright _playwright;
        private IBrowser _browser;
        private IBrowserContext _context;
        private string _runtimeUserDataDir;
        private bool _cleanupRuntimeUserDataDir;
        private readonly List<IPage> _tabs = new List<IPage>();
        private IPage _currentPage;

        public PwBrowser BrowserOpen(
            out PwTab initialTab,
            bool headless = false,
            string browserPath = null,
            bool startMaximized = true,
            int? remoteDebuggingPort = null,
            bool useSystemDir = true,
            string userDataDir = null)
        {
            CloseBrowser();
            EnsurePlaywrightDriverSearchPath();
            initialTab = null;

            if (!string.IsNullOrWhiteSpace(browserPath) && !File.Exists(browserPath))
            {
                throw new FileNotFoundException("browserPath 指定的浏览器可执行文件不存在。", browserPath);
            }

            var args = new List<string>();
            if (startMaximized)
            {
                args.Add("--start-maximized");
            }

            if (remoteDebuggingPort.HasValue)
            {
                args.Add("--remote-debugging-port=" + remoteDebuggingPort.Value);
            }

            var executablePath = string.IsNullOrWhiteSpace(browserPath) ? null : browserPath;
            var effectiveUserDataDir = ResolveUserDataDir(useSystemDir, userDataDir, out var cleanupAfterClose);
            _runtimeUserDataDir = effectiveUserDataDir;
            _cleanupRuntimeUserDataDir = cleanupAfterClose;
            var usePersistentContext = !string.IsNullOrWhiteSpace(effectiveUserDataDir);

            if (useSystemDir)
            {
                var existingEdgeCount = GetRunningEdgePids().Count;
                if (existingEdgeCount > 0)
                {
                    var killedCount = KillAllEdgeProcesses();
                    Thread.Sleep(2000);
                    if (killedCount > 0)
                    {
                        TrySetEdgeExitTypeNormal(effectiveUserDataDir);
                    }
                }
            }

            try
            {
                LaunchBrowserContext(headless, executablePath, startMaximized, args, usePersistentContext, effectiveUserDataDir);
            }
            catch (PlaywrightException ex) when (useSystemDir)
            {
                CloseBrowser();

                // UseSystemDir=True 时，首次失败后尝试清理残留 Edge 进程并重试一次。
                var killedCount = KillAllEdgeProcesses();
                Thread.Sleep(2000);
                if (killedCount > 0)
                {
                    TrySetEdgeExitTypeNormal(effectiveUserDataDir);
                }

                try
                {
                    LaunchBrowserContext(headless, executablePath, startMaximized, args, usePersistentContext, effectiveUserDataDir);
                }
                catch (PlaywrightException retryEx)
                {
                    CloseBrowser();
                    var message = BuildUseSystemDirFailureMessage(retryEx, effectiveUserDataDir)
                        + "\n- 已执行自动恢复: 结束 msedge 进程 " + killedCount + " 个，等待 2000ms 后重试 1 次。"
                        + "\n- 首次失败错误: " + (string.IsNullOrWhiteSpace(ex.Message) ? "<empty>" : ex.Message.Trim());
                    throw new InvalidOperationException(message, retryEx);
                }
            }

            initialTab = PrepareInitialOpenedTab();
            return new PwBrowser(this);
        }

        /// <summary>
        /// 启动后选定上下文中<strong>首个</strong>页面为当前 Tab，置顶并等待其触发 <see cref="LoadState.Load"/>。
        /// 若无任何页面（极少见），则新建空白页再放入列表首位。
        /// </summary>
        private PwTab PrepareInitialOpenedTab()
        {
            if (_context == null)
            {
                throw new InvalidOperationException("浏览器上下文未初始化。");
            }

            SyncTabsFromContext();

            if (_tabs.Count == 0)
            {
                var page = _context.NewPageAsync().GetAwaiter().GetResult();
                RegisterPage(page, switchToNewPage: true);
            }

            var firstPage = _tabs[0];
            _currentPage = firstPage;

            try
            {
                firstPage.BringToFrontAsync().GetAwaiter().GetResult();
            }
            catch
            {
            }

            firstPage.WaitForLoadStateAsync(LoadState.Load).GetAwaiter().GetResult();

            return WrapTab(firstPage);
        }

        private void LaunchBrowserContext(
            bool headless,
            string executablePath,
            bool startMaximized,
            List<string> args,
            bool usePersistentContext,
            string effectiveUserDataDir)
        {
            _playwright = Microsoft.Playwright.Playwright.CreateAsync().GetAwaiter().GetResult();

            if (usePersistentContext)
            {
                Directory.CreateDirectory(effectiveUserDataDir);
                var persistentOptions = new BrowserTypeLaunchPersistentContextOptions
                {
                    Headless = headless,
                    ExecutablePath = executablePath,
                    Args = args.Count == 0 ? null : args,
                    IgnoreDefaultArgs = new[] { "--enable-automation" },
                    ViewportSize = startMaximized ? ViewportSize.NoViewport : null
                };
                _context = _playwright.Chromium
                    .LaunchPersistentContextAsync(effectiveUserDataDir, persistentOptions)
                    .GetAwaiter()
                    .GetResult();
                _browser = _context.Browser;
                return;
            }

            var launchOptions = new BrowserTypeLaunchOptions
            {
                Headless = headless,
                ExecutablePath = executablePath,
                Args = args.Count == 0 ? null : args,
                IgnoreDefaultArgs = new[] { "--enable-automation" }
            };

            _browser = _playwright.Chromium.LaunchAsync(launchOptions).GetAwaiter().GetResult();
            var contextOptions = startMaximized
                ? new BrowserNewContextOptions { ViewportSize = ViewportSize.NoViewport }
                : null;
            _context = _browser.NewContextAsync(contextOptions).GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            CloseBrowser();
        }

        internal IReadOnlyList<CookieInfo> GetBrowserCookiesInfo()
        {
            EnsureContextReady();
            var result = new Dictionary<string, CookieInfo>();
            foreach (var context in _browser.Contexts)
            {
                var cookies = context.CookiesAsync().GetAwaiter().GetResult();
                foreach (var cookie in cookies)
                {
                    var key = $"{cookie.Name}|{cookie.Domain}|{cookie.Path}";
                    result[key] = ToCookieInfo(cookie);
                }
            }

            return result.Values.ToList();
        }

        internal PwTab NewTab(string url = null, double? timeout = null)
        {
            EnsureBrowserOpened();
            var page = _context.NewPageAsync().GetAwaiter().GetResult();
            RegisterPage(page, true);
            if (!string.IsNullOrWhiteSpace(url))
            {
                page.GotoAsync(url, new PageGotoOptions
                {
                    Timeout = timeout.HasValue ? (float?)timeout.Value : null
                }).GetAwaiter().GetResult();
            }

            return WrapTab(page);
        }

        internal PwTab[] GetAllTabs()
        {
            EnsureContextReady();
            SyncTabsFromContext();
            return _tabs.Select(WrapTab).ToArray();
        }

        internal PwTab SwitchTab(int index)
        {
            return SwitchTab(index, null, null, null, null, null);
        }

        internal PwTab SwitchTab(PwTab tab)
        {
            return SwitchTab(null, null, null, null, null, tab);
        }

        internal PwTab SwitchTab(int? index, string title, string titleRe, string url, string urlRe, PwTab tab = null)
        {
            EnsureContextReady();
            SyncTabsFromContext();
            if (_tabs.Count == 0)
            {
                throw new InvalidOperationException("当前没有可切换的标签页。");
            }

            IPage target = null;
            if (tab != null)
            {
                var sourcePage = tab.Page;
                if (sourcePage == null || sourcePage.IsClosed)
                {
                    throw new InvalidOperationException("输入的 Tab 无效或已关闭。");
                }

                target = _tabs.FirstOrDefault(p => p == sourcePage);
                if (target == null)
                {
                    throw new InvalidOperationException("输入的 Tab 不属于当前 Browser 上下文。");
                }
            }
            else if (index.HasValue)
            {
                if (index.Value < 0 || index.Value >= _tabs.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), $"Tab index out of range: {index.Value}.");
                }

                target = _tabs[index.Value];
            }
            else
            {
                var titleRegex = string.IsNullOrWhiteSpace(titleRe) ? null : new Regex(titleRe);
                var urlRegex = string.IsNullOrWhiteSpace(urlRe) ? null : new Regex(urlRe);
                foreach (var page in _tabs)
                {
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        var pageTitle = page.TitleAsync().GetAwaiter().GetResult();
                        if (!string.Equals(pageTitle, title, StringComparison.Ordinal))
                        {
                            continue;
                        }
                    }

                    if (titleRegex != null)
                    {
                        var pageTitle = page.TitleAsync().GetAwaiter().GetResult();
                        if (!titleRegex.IsMatch(pageTitle ?? string.Empty))
                        {
                            continue;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(url) &&
                        !string.Equals(page.Url, url, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (urlRegex != null && !urlRegex.IsMatch(page.Url ?? string.Empty))
                    {
                        continue;
                    }

                    target = page;
                    break;
                }
            }

            if (target == null)
            {
                throw new InvalidOperationException("未找到满足条件的标签页。");
            }

            _currentPage = target;
            _currentPage.BringToFrontAsync().GetAwaiter().GetResult();
            return WrapTab(_currentPage);
        }

        internal PwTab GetLatestTab()
        {
            EnsureContextReady();
            SyncTabsFromContext();
            if (_tabs.Count == 0)
            {
                return null;
            }

            _currentPage = _tabs[_tabs.Count - 1];
            _currentPage.BringToFrontAsync().GetAwaiter().GetResult();
            return WrapTab(_currentPage);
        }

        internal PwTab GetActivatedTab()
        {
            EnsureContextReady();
            SyncTabsFromContext();
            if (_tabs.Count == 0)
            {
                return null;
            }

            if (_currentPage != null && !_currentPage.IsClosed && _tabs.Contains(_currentPage))
            {
                return WrapTab(_currentPage);
            }

            _currentPage = _tabs.LastOrDefault();
            return _currentPage == null ? null : WrapTab(_currentPage);
        }

        internal PwTab WrapTab(IPage page)
        {
            return new PwTab(this, page);
        }

        internal IReadOnlyList<CookieInfo> GetTabCookiesInfo(IPage page)
        {
            var currentUrl = page.Url;
            var cookies = string.IsNullOrWhiteSpace(currentUrl)
                ? page.Context.CookiesAsync().GetAwaiter().GetResult()
                : page.Context.CookiesAsync(new[] { currentUrl }).GetAwaiter().GetResult();

            return cookies.Select(ToCookieInfo).ToList();
        }

        internal Dictionary<string, string> GetTabLocalStorage(IPage page)
        {
            EnsureTabAlive(page);
            return ReadStorage(page, "localStorage");
        }

        internal Dictionary<string, string> GetTabSessionStorage(IPage page)
        {
            EnsureTabAlive(page);
            return ReadStorage(page, "sessionStorage");
        }

        private static Dictionary<string, string> ReadStorage(IPage page, string storageName)
        {
            try
            {
                var script =
                    "(name) => {" +
                    "  try {" +
                    "    var storage = name === 'localStorage' ? window.localStorage : window.sessionStorage;" +
                    "    var result = {};" +
                    "    for (var i = 0; i < storage.length; i++) {" +
                    "      var key = storage.key(i);" +
                    "      if (key !== null) result[key] = storage.getItem(key);" +
                    "    }" +
                    "    return JSON.stringify(result);" +
                    "  } catch (e) {" +
                    "    return '{}';" +
                    "  }" +
                    "}";

                var json = page.EvaluateAsync<string>(script, storageName).GetAwaiter().GetResult();
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new Dictionary<string, string>();
                }

                using (var doc = JsonDocument.Parse(json))
                {
                    var values = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        values[prop.Name] = prop.Value.ValueKind == JsonValueKind.Null
                            ? string.Empty
                            : prop.Value.GetString() ?? string.Empty;
                    }

                    return values;
                }
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }

        internal void RegisterPage(IPage page, bool switchToNewPage)
        {
            SyncTabsFromContext();
            if (!_tabs.Contains(page))
            {
                _tabs.Add(page);
            }

            if (switchToNewPage)
            {
                _currentPage = page;
            }
        }

        internal void UnregisterClosedTab(IPage page)
        {
            SyncTabsFromContext();
            _tabs.RemoveAll(p => p == page || p.IsClosed);
            if (_currentPage == page || (_currentPage != null && _currentPage.IsClosed))
            {
                _currentPage = _tabs.LastOrDefault();
            }
        }

        internal void EnsureTabAlive(IPage page)
        {
            if (page == null || page.IsClosed)
            {
                throw new InvalidOperationException("Tab 已关闭或无效。");
            }

            // 将“正在被调用的 tab”同步为当前 tab，避免 Browser 级 API（如 GetSessionStorage）
            // 仍读取到历史 _currentPage 导致上下文不一致。
            SyncTabsFromContext();
            if (!_tabs.Contains(page))
            {
                _tabs.Add(page);
            }

            _currentPage = page;
        }

        private static string BuildUseSystemDirFailureMessage(PlaywrightException ex, string userDataDir)
        {
            var edgePids = GetRunningEdgePids();
            var pidText = edgePids.Count == 0 ? "无" : string.Join(", ", edgePids);
            var raw = (ex?.Message ?? string.Empty).Trim();
            var lockHint = IsUserDataDirInUseError(raw)
                ? "检测到目录占用特征（user data dir in use / singleton lock）。"
                : "未检测到明确的目录占用特征，可能是权限、企业策略或浏览器参数冲突。";

            return
                "使用 UseSystemDir=True 启动失败。" +
                "\n- UserDataDir: " + (string.IsNullOrWhiteSpace(userDataDir) ? "<empty>" : userDataDir) +
                "\n- 当前 msedge 进程 PID: " + pidText +
                "\n- 诊断: " + lockHint +
                "\n- Playwright 原始错误: " + (string.IsNullOrWhiteSpace(raw) ? "<empty>" : raw) +
                "\n建议：若要稳定自动化，优先使用 UseSystemDir=False（隔离环境）；如必须用系统目录，请先彻底结束所有 msedge 进程后重试。";
        }

        private static List<int> GetRunningEdgePids()
        {
            try
            {
                return Process.GetProcessesByName("msedge")
                    .Select(p => p.Id)
                    .OrderBy(id => id)
                    .ToList();
            }
            catch
            {
                return new List<int>();
            }
        }

        private static int KillAllEdgeProcesses()
        {
            var killed = 0;
            try
            {
                foreach (var p in Process.GetProcessesByName("msedge"))
                {
                    try
                    {
                        p.Kill();
                        killed++;
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

            return killed;
        }

        private static void TrySetEdgeExitTypeNormal(string userDataDir)
        {
            try
            {
                foreach (var preferencesPath in GetEdgePreferencesPaths(userDataDir))
                {
                    try
                    {
                        if (!TryRewritePreferencesExitTypeToNormal(preferencesPath))
                        {
                            continue;
                        }

                        string profileFolder = Path.GetDirectoryName(preferencesPath);
                        TryDeleteAllFilesUnderSessionsFolder(profileFolder);
                    }
                    catch
                    {
                        // 某些 Profile 可能被占用；忽略单 Profile 失败，避免影响主流程。
                    }
                }
            }
            catch
            {
                // 仅做最佳努力修复，失败不阻断 OpenBrowser 主流程。
            }
        }

        private static IEnumerable<string> GetEdgePreferencesPaths(string userDataDir)
        {
            var paths = new List<string>();
            if (string.IsNullOrWhiteSpace(userDataDir) || !Directory.Exists(userDataDir))
            {
                return paths;
            }

            var directPreferences = Path.Combine(userDataDir, "Preferences");
            if (File.Exists(directPreferences))
            {
                paths.Add(directPreferences);
            }

            try
            {
                foreach (var profileDir in Directory.GetDirectories(userDataDir))
                {
                    var profileName = Path.GetFileName(profileDir);
                    if (!string.Equals(profileName, "Default", StringComparison.OrdinalIgnoreCase) &&
                        !profileName.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var preferencesPath = Path.Combine(profileDir, "Preferences");
                    if (File.Exists(preferencesPath))
                    {
                        paths.Add(preferencesPath);
                    }
                }
            }
            catch
            {
            }

            return paths;
        }

        /// <summary>
        /// 与 <paramref name="preferencesContainingDirectory"/> 同级的 Sessions 文件夹下，尽最大努力递归删除<strong>文件</strong>；子目录保留不删。
        /// </summary>
        private static void TryDeleteAllFilesUnderSessionsFolder(string preferencesContainingDirectory)
        {
            if (string.IsNullOrWhiteSpace(preferencesContainingDirectory) ||
                !Directory.Exists(preferencesContainingDirectory))
            {
                return;
            }

            string sessionsRoot = Path.Combine(preferencesContainingDirectory, "Sessions");
            if (!Directory.Exists(sessionsRoot))
            {
                return;
            }

            try
            {
                DeleteFilesUnderDirectoryRecursiveBestEffort(sessionsRoot);
            }
            catch
            {
                // Sessions 可能被其它进程短时占用；不阻断浏览器启动。
            }
        }

        private static void DeleteFilesUnderDirectoryRecursiveBestEffort(string directoryPath)
        {
            try
            {
                foreach (string file in Directory.GetFiles(directoryPath))
                {
                    TryDeleteFileBestEffort(file);
                }
            }
            catch
            {
            }

            IEnumerable<string> subDirs = Array.Empty<string>();
            try
            {
                subDirs = Directory.GetDirectories(directoryPath);
            }
            catch
            {
                return;
            }

            foreach (var subDir in subDirs)
            {
                DeleteFilesUnderDirectoryRecursiveBestEffort(subDir);
            }
        }

        private static void TryDeleteFileBestEffort(string fullPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
                {
                    return;
                }

                var attrs = File.GetAttributes(fullPath);
                if ((attrs & FileAttributes.ReadOnly) != 0)
                {
                    File.SetAttributes(fullPath, attrs & ~FileAttributes.ReadOnly);
                }

                File.Delete(fullPath);
            }
            catch
            {
                // 单次删除失败不影响其它文件。
            }
        }

        /// <returns>true 时已向磁盘写回修改后的 Preferences；未修改或跳过则为 false。</returns>
        private static bool TryRewritePreferencesExitTypeToNormal(string preferencesPath)
        {
            if (string.IsNullOrWhiteSpace(preferencesPath) || !File.Exists(preferencesPath))
            {
                return false;
            }

            var content = File.ReadAllText(preferencesPath);
            if (string.IsNullOrWhiteSpace(content))
            {
                return false;
            }

            using (var doc = JsonDocument.Parse(content))
            using (var stream = new MemoryStream())
            {
                var updated = false;
                using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                {
                    WriteJsonWithExitTypeOverride(doc.RootElement, writer, ref updated);
                }

                if (!updated)
                {
                    return false;
                }

                File.WriteAllBytes(preferencesPath, stream.ToArray());
                return true;
            }
        }

        private static void WriteJsonWithExitTypeOverride(JsonElement element, Utf8JsonWriter writer, ref bool updated)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    writer.WriteStartObject();
                    foreach (var property in element.EnumerateObject())
                    {
                        if (string.Equals(property.Name, "exit_type", StringComparison.OrdinalIgnoreCase))
                        {
                            writer.WriteString(property.Name, "Normal");
                            updated = true;
                            continue;
                        }

                        writer.WritePropertyName(property.Name);
                        WriteJsonWithExitTypeOverride(property.Value, writer, ref updated);
                    }
                    writer.WriteEndObject();
                    break;
                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    foreach (var item in element.EnumerateArray())
                    {
                        WriteJsonWithExitTypeOverride(item, writer, ref updated);
                    }
                    writer.WriteEndArray();
                    break;
                case JsonValueKind.String:
                    writer.WriteStringValue(element.GetString());
                    break;
                case JsonValueKind.Number:
                    if (element.TryGetInt32(out var i32))
                    {
                        writer.WriteNumberValue(i32);
                    }
                    else if (element.TryGetInt64(out var i64))
                    {
                        writer.WriteNumberValue(i64);
                    }
                    else if (element.TryGetDecimal(out var dec))
                    {
                        writer.WriteNumberValue(dec);
                    }
                    else
                    {
                        writer.WriteNumberValue(element.GetDouble());
                    }
                    break;
                case JsonValueKind.True:
                    writer.WriteBooleanValue(true);
                    break;
                case JsonValueKind.False:
                    writer.WriteBooleanValue(false);
                    break;
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                default:
                    writer.WriteNullValue();
                    break;
            }
        }

        private static bool IsUserDataDirInUseError(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            var m = message.ToLowerInvariant();
            return m.Contains("user data directory is already in use")
                || m.Contains("process_singleton")
                || m.Contains("singletonlock")
                || m.Contains("profile appears to be in use")
                || m.Contains("can not read and write to its data directory");
        }

        internal void CloseBrowser()
        {
            try
            {
                if (_context != null)
                {
                    _context.CloseAsync().GetAwaiter().GetResult();
                }
            }
            catch
            {
            }

            try
            {
                if (_browser != null)
                {
                    _browser.CloseAsync().GetAwaiter().GetResult();
                }
            }
            catch
            {
            }

            try
            {
                if (_playwright != null)
                {
                    _playwright.Dispose();
                }
            }
            catch
            {
            }

            _tabs.Clear();
            _currentPage = null;
            _context = null;
            _browser = null;
            _playwright = null;

            TryCleanupRuntimeUserDataDir();

            // 关闭阶段尽最大努力清理资源；即便某一步失败，也不阻断后续流程。
        }

        private string ResolveUserDataDir(bool useSystemDir, string userDataDir, out bool cleanupAfterClose)
        {
            cleanupAfterClose = false;

            if (!useSystemDir)
            {
                if (!string.IsNullOrWhiteSpace(userDataDir))
                {
                    return Path.GetFullPath(userDataDir);
                }

                cleanupAfterClose = true;
                return CreateIsolatedTempUserDataDir();
            }

            // 使用系统 Edge 用户目录（持久化 Profile）。
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(localAppData))
            {
                return Path.Combine(localAppData, "Microsoft", "Edge", "User Data");
            }

            return null;
        }

        private static string CreateIsolatedTempUserDataDir()
        {
            var root = Path.Combine(Path.GetTempPath(), "OpenRPA.Playwright", "profiles");
            Directory.CreateDirectory(root);
            return Path.Combine(root, Guid.NewGuid().ToString("N"));
        }

        private void TryCleanupRuntimeUserDataDir()
        {
            var targetDir = _runtimeUserDataDir;
            var shouldCleanup = _cleanupRuntimeUserDataDir;
            _runtimeUserDataDir = null;
            _cleanupRuntimeUserDataDir = false;

            if (!shouldCleanup || string.IsNullOrWhiteSpace(targetDir) || !Directory.Exists(targetDir))
            {
                return;
            }

            try
            {
                Directory.Delete(targetDir, true);
            }
            catch
            {
                // 某些 Edge 子进程退出会有短延迟；清理失败时忽略，不影响主流程。
            }
        }

        internal static void ApplyDelay(int delayBeforeMs)
        {
            if (delayBeforeMs > 0)
            {
                Thread.Sleep(delayBeforeMs);
            }
        }

        internal static WaitForSelectorState? ParseWaitState(string waitState)
        {
            if (string.IsNullOrWhiteSpace(waitState))
            {
                return null;
            }

            switch (waitState.Trim().ToLowerInvariant())
            {
                case "visible":
                    return WaitForSelectorState.Visible;
                case "attached":
                    return WaitForSelectorState.Attached;
                case "hidden":
                    return WaitForSelectorState.Hidden;
                case "detached":
                    return WaitForSelectorState.Detached;
                default:
                    throw new ArgumentException("不支持的 waitState: " + waitState);
            }
        }

        internal static IReadOnlyList<KeyboardModifier> ParseModifiers(string[] modifiers)
        {
            if (modifiers == null || modifiers.Length == 0)
            {
                return null;
            }

            var result = new List<KeyboardModifier>();
            foreach (var item in modifiers)
            {
                if (string.IsNullOrWhiteSpace(item))
                {
                    continue;
                }

                switch (item.Trim().ToLowerInvariant())
                {
                    case "alt":
                        result.Add(KeyboardModifier.Alt);
                        break;
                    case "control":
                    case "ctrl":
                        result.Add(KeyboardModifier.Control);
                        break;
                    case "meta":
                        result.Add(KeyboardModifier.Meta);
                        break;
                    case "shift":
                        result.Add(KeyboardModifier.Shift);
                        break;
                    default:
                        throw new ArgumentException("不支持的 modifier: " + item);
                }
            }

            return result;
        }

        private void EnsureBrowserOpened()
        {
            if (_playwright == null || _browser == null || !_browser.IsConnected)
            {
                throw new InvalidOperationException("Browser is not opened. Call BrowserOpen() first.");
            }
        }

        private void EnsureContextReady()
        {
            EnsureBrowserOpened();
            if (_context == null)
            {
                _context = _browser.NewContextAsync().GetAwaiter().GetResult();
            }
        }

        private void SyncTabsFromContext()
        {
            if (_context == null)
            {
                return;
            }

            _tabs.Clear();
            _tabs.AddRange(_context.Pages.Where(p => !p.IsClosed));

            if (_currentPage != null && _currentPage.IsClosed)
            {
                _currentPage = null;
            }

            if (_currentPage == null && _tabs.Count > 0)
            {
                _currentPage = _tabs[0];
            }
        }

        private static CookieInfo ToCookieInfo(BrowserContextCookiesResult cookie)
        {
            return new CookieInfo
            {
                Name = cookie.Name,
                Value = cookie.Value,
                Domain = cookie.Domain,
                Path = cookie.Path,
                Expires = cookie.Expires,
                HttpOnly = cookie.HttpOnly,
                Secure = cookie.Secure,
                SameSite = cookie.SameSite.ToString()
            };
        }

        private static void EnsurePlaywrightDriverSearchPath()
        {
            const string envName = "PLAYWRIGHT_DRIVER_SEARCH_PATH";
            var configured = Environment.GetEnvironmentVariable(envName);
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return;
            }

            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrWhiteSpace(documents))
            {
                return;
            }

            var openRpaRoot = Path.Combine(documents, "OpenRPA");
            var extensionsRoot = Path.Combine(openRpaRoot, "extensions");
            var nodePath = Path.Combine(extensionsRoot, ".playwright", "node", "win32_x64", "node.exe");
            if (File.Exists(nodePath))
            {
                Environment.SetEnvironmentVariable(envName, extensionsRoot);
            }
        }
    }

    public sealed class PwBrowser
    {
        private readonly PlaywrightSyncClient _client;

        internal PwBrowser(PlaywrightSyncClient client)
        {
            _client = client;
        }

        public PwTab NewTab(string url = null, double? timeout = null)
        {
            return _client.NewTab(url, timeout);
        }

        public PwTab[] GetAllTab()
        {
            return _client.GetAllTabs();
        }

        public PwTab SwitchTab(int index)
        {
            return _client.SwitchTab(index);
        }

        public PwTab SwitchTab(int? index = null, string title = null, string titleRe = null, string url = null, string urlRe = null, PwTab tab = null)
        {
            return _client.SwitchTab(index, title, titleRe, url, urlRe, tab);
        }

        public PwTab GetLatestTab()
        {
            return _client.GetLatestTab();
        }

        public PwTab GetActivatedTab()
        {
            return _client.GetActivatedTab();
        }

        public Cookies GetCookies()
        {
            return new Cookies(_client.GetBrowserCookiesInfo());
        }

        public Storages GetLocalStorage()
        {
            var tab = _client.GetActivatedTab();
            if (tab == null)
            {
                return new Storages(new Dictionary<string, string>());
            }

            return tab.GetLocalStorage();
        }

        public Storages GetSessionStorage()
        {
            var tab = _client.GetActivatedTab();
            if (tab == null)
            {
                return new Storages(new Dictionary<string, string>());
            }

            return tab.GetSessionStorage();
        }

        public void Close()
        {
            _client.CloseBrowser();
        }
    }

    public sealed class PwTab
    {
        private readonly PlaywrightSyncClient _client;
        private readonly IPage _page;

        internal PwTab(PlaywrightSyncClient client, IPage page)
        {
            _client = client;
            _page = page;
        }

        internal IPage Page => _page;

        /// <summary>Current document URL; read fresh from the page each access.</summary>
        public string Url
        {
            get
            {
                if (_page == null)
                {
                    return string.Empty;
                }

                if (_page.IsClosed)
                {
                    return _page.Url ?? string.Empty;
                }

                _client.EnsureTabAlive(_page);
                return _page.Url ?? string.Empty;
            }
        }

        /// <summary>Document title; read fresh each access (async API wrapped synchronously).</summary>
        public string Title
        {
            get
            {
                if (_page == null || _page.IsClosed)
                {
                    return string.Empty;
                }

                _client.EnsureTabAlive(_page);
                return _page.TitleAsync().GetAwaiter().GetResult() ?? string.Empty;
            }
        }

        /// <summary>Whether this tab is closed.</summary>
        public bool IsClosed => _page == null || _page.IsClosed;

        /// <summary>Full page HTML; read fresh each access.</summary>
        public string Html
        {
            get
            {
                if (_page == null || _page.IsClosed)
                {
                    return string.Empty;
                }

                _client.EnsureTabAlive(_page);
                return _page.ContentAsync().GetAwaiter().GetResult() ?? string.Empty;
            }
        }

        public void Activate()
        {
            _client.EnsureTabAlive(_page);
            _page.BringToFrontAsync().GetAwaiter().GetResult();
            _client.RegisterPage(_page, true);
        }

        public PwElement FindElement(string selector, int index = 0, double? timeout = null, string waitState = null, int delayBefore = 300)
        {
            _client.EnsureTabAlive(_page);
            PlaywrightSyncClient.ApplyDelay(delayBefore);

            var locatorSet = _page.Locator(selector);
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "index 不能小于 0。");
            }

            var target = locatorSet.Nth(index);
            var parsedState = PlaywrightSyncClient.ParseWaitState(waitState);
            if (parsedState.HasValue || timeout.HasValue)
            {
                target.WaitForAsync(new LocatorWaitForOptions
                {
                    Timeout = timeout.HasValue ? (float?)timeout.Value : null,
                    State = parsedState
                }).GetAwaiter().GetResult();
            }

            var count = locatorSet.CountAsync().GetAwaiter().GetResult();
            if (count <= index)
            {
                throw new InvalidOperationException($"FindElement 未找到匹配元素，selector={selector}, index={index}。");
            }

            return new PwElement(_client, _page, target);
        }

        public PwLocator Locate(string selector, double? timeout = null, int delayBefore = 300)
        {
            _client.EnsureTabAlive(_page);
            if (string.IsNullOrWhiteSpace(selector))
            {
                throw new ArgumentException("selector 不能为空。", nameof(selector));
            }

            PlaywrightSyncClient.ApplyDelay(delayBefore);
            var locator = _page.Locator(selector);
            if (timeout.HasValue)
            {
                locator.First.WaitForAsync(new LocatorWaitForOptions
                {
                    Timeout = (float)Math.Max(0, timeout.Value),
                    State = WaitForSelectorState.Attached
                }).GetAwaiter().GetResult();
            }

            return new PwLocator(_client, _page, locator);
        }

        public bool ElementExists(string selector, int index = 0)
        {
            _client.EnsureTabAlive(_page);
            var count = _page.Locator(selector).CountAsync().GetAwaiter().GetResult();
            return count > index;
        }

        public T RunJs<T>(string script, object arg = null)
        {
            _client.EnsureTabAlive(_page);
            return _page.EvaluateAsync<T>(script, arg).GetAwaiter().GetResult();
        }

        public Cookies GetCookies()
        {
            _client.EnsureTabAlive(_page);
            return new Cookies(_client.GetTabCookiesInfo(_page));
        }

        public Storages GetLocalStorage()
        {
            return new Storages(_client.GetTabLocalStorage(_page));
        }

        public Storages GetSessionStorage()
        {
            return new Storages(_client.GetTabSessionStorage(_page));
        }

        public void NavigateUrl(string url, double? timeout = null)
        {
            _client.EnsureTabAlive(_page);
            _page.GotoAsync(url, new PageGotoOptions
            {
                Timeout = timeout.HasValue ? (float?)timeout.Value : null
            }).GetAwaiter().GetResult();
        }

        public void SendKeys(string keys, int? delay = null)
        {
            _client.EnsureTabAlive(_page);
            if (string.IsNullOrWhiteSpace(keys))
            {
                throw new ArgumentException("keys 不能为空。", nameof(keys));
            }

            _page.Keyboard.PressAsync(keys, new KeyboardPressOptions
            {
                Delay = delay.HasValue ? (float?)delay.Value : null
            }).GetAwaiter().GetResult();
        }

        public void Back()
        {
            _client.EnsureTabAlive(_page);
            _page.GoBackAsync().GetAwaiter().GetResult();
        }

        public void Forward()
        {
            _client.EnsureTabAlive(_page);
            _page.GoForwardAsync().GetAwaiter().GetResult();
        }

        public void Refresh()
        {
            _client.EnsureTabAlive(_page);
            _page.ReloadAsync().GetAwaiter().GetResult();
        }

        public void Close()
        {
            if (_page != null && !_page.IsClosed)
            {
                _page.CloseAsync().GetAwaiter().GetResult();
            }

            _client.UnregisterClosedTab(_page);
        }

        public TabInfo GetInfo()
        {
            return new TabInfo
            {
                Url = Url,
                Title = Title,
                IsClosed = IsClosed
            };
        }
    }

    public sealed class PwLocator
    {
        private readonly PlaywrightSyncClient _client;
        private readonly IPage _page;
        private readonly ILocator _locator;

        internal PwLocator(PlaywrightSyncClient client, IPage page, ILocator locator)
        {
            _client = client;
            _page = page;
            _locator = locator ?? throw new ArgumentNullException(nameof(locator));
        }

        public int Count()
        {
            EnsureAlive();
            return _locator.CountAsync().GetAwaiter().GetResult();
        }

        public PwLocator Locate(string selector, double? timeout = null, int delayBefore = 0)
        {
            EnsureAlive();
            if (string.IsNullOrWhiteSpace(selector))
            {
                throw new ArgumentException("selector 不能为空。", nameof(selector));
            }

            PlaywrightSyncClient.ApplyDelay(delayBefore);
            var child = _locator.Locator(selector);
            if (timeout.HasValue)
            {
                child.First.WaitForAsync(new LocatorWaitForOptions
                {
                    Timeout = (float)Math.Max(0, timeout.Value),
                    State = WaitForSelectorState.Attached
                }).GetAwaiter().GetResult();
            }

            return new PwLocator(_client, _page, child);
        }

        public PwLocator First()
        {
            EnsureAlive();
            return new PwLocator(_client, _page, _locator.First);
        }

        public PwLocator Nth(int index)
        {
            EnsureAlive();
            return new PwLocator(_client, _page, _locator.Nth(index));
        }

        public ILocator AsNative()
        {
            return _locator;
        }

        private void EnsureAlive()
        {
            _client.EnsureTabAlive(_page);
        }
    }

    public sealed class PwElement
    {
        private readonly PlaywrightSyncClient _client;
        private readonly IPage _page;
        private readonly ILocator _locator;

        internal PwElement(PlaywrightSyncClient client, IPage page, ILocator locator)
        {
            _client = client;
            _page = page;
            _locator = locator;
        }

        /// <summary>元素标签名 (<c>tagName</c>)，每次访问即时读取。</summary>
        public string Tag =>
            ReadStringWhenTabAlive(() =>
                _locator.EvaluateAsync<string>("el => el.tagName").GetAwaiter().GetResult());

        /// <summary>元素外层 HTML (<c>outerHTML</c>)，每次访问即时读取。</summary>
        public string Html =>
            ReadStringWhenTabAlive(() =>
                _locator.EvaluateAsync<string>("el => el.outerHTML").GetAwaiter().GetResult());

        /// <summary>元素内部 HTML (<c>innerHTML</c>)，每次访问即时读取。</summary>
        public string InnerHtml =>
            ReadStringWhenTabAlive(() => _locator.InnerHTMLAsync().GetAwaiter().GetResult());

        /// <summary>
        /// 仅当前元素「直接子级」文本节点拼接（不含子元素内部的文字），每次访问即时读取。
        /// </summary>
        public string Text =>
            ReadStringWhenTabAlive(() =>
                _locator.EvaluateAsync<string>(
                        "el => { if (!el) return ''; let s = ''; for (const n of el.childNodes) { if (n.nodeType === 3) s += n.textContent; } return s; }")
                    .GetAwaiter().GetResult());

        /// <summary>
        /// 当前元素及其所有后代在页面上的渲染文本（DOM <c>innerText</c>，含子标签内文字），每次访问即时读取。
        /// </summary>
        public string InnerText =>
            ReadStringWhenTabAlive(() => _locator.InnerTextAsync().GetAwaiter().GetResult());

        public bool Exists()
        {
            try
            {
                return _locator.CountAsync().GetAwaiter().GetResult() > 0;
            }
            catch
            {
                return false;
            }
        }

        public void Click(
            MouseButton button = MouseButton.Left,
            int count = 1,
            int interval = 500,
            string[] modifiers = null,
            bool force = false,
            ClickValidateMode validate = ClickValidateMode.None,
            string validationSelector = null,
            int timeout = 15000)
        {
            ExecuteClickWithValidation(
                clickAction: () =>
                {
                    _locator.ClickAsync(new LocatorClickOptions
                    {
                        Button = button,
                        ClickCount = count,
                        Modifiers = PlaywrightSyncClient.ParseModifiers(modifiers),
                        Force = force
                    }).GetAwaiter().GetResult();
                    return true;
                },
                validate: validate,
                validationSelector: validationSelector,
                interval: interval,
                timeout: timeout,
                operationName: "Click");
        }

        public void DoubleClick(
            MouseButton button = MouseButton.Left,
            int count = 1,
            int interval = 500,
            string[] modifiers = null,
            bool force = false,
            ClickValidateMode validate = ClickValidateMode.None,
            string validationSelector = null,
            int timeout = 15000)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "count 必须大于 0。");
            }

            ExecuteClickWithValidation(
                clickAction: () =>
                {
                    for (var i = 0; i < count; i++)
                    {
                        _locator.DblClickAsync(new LocatorDblClickOptions
                        {
                            Button = button,
                            Modifiers = PlaywrightSyncClient.ParseModifiers(modifiers),
                            Force = force
                        }).GetAwaiter().GetResult();
                    }

                    return true;
                },
                validate: validate,
                validationSelector: validationSelector,
                interval: interval,
                timeout: timeout,
                operationName: "DoubleClick");
        }

        public PwTab ClickForNewTab(
            MouseButton button = MouseButton.Left,
            int count = 1,
            int interval = 500,
            string[] modifiers = null,
            bool force = false,
            ClickValidateMode validate = ClickValidateMode.None,
            string validationSelector = null,
            int timeout = 15000)
        {
            _client.EnsureTabAlive(_page);
            return ExecuteClickWithValidation(
                clickAction: () =>
                {
                    var newPage = _page.Context.RunAndWaitForPageAsync(() => _locator.ClickAsync(new LocatorClickOptions
                    {
                        Button = button,
                        ClickCount = count,
                        Modifiers = PlaywrightSyncClient.ParseModifiers(modifiers),
                        Force = force
                    })).GetAwaiter().GetResult();
                    _client.RegisterPage(newPage, true);
                    return _client.WrapTab(newPage);
                },
                validate: validate,
                validationSelector: validationSelector,
                interval: interval,
                timeout: timeout,
                operationName: "ClickForNewTab");
        }

        public DownloadInfo ClickForDownload(
            string saveAsPath = null,
            MouseButton button = MouseButton.Left,
            int count = 1,
            int interval = 500,
            string[] modifiers = null,
            bool force = false,
            ClickValidateMode validate = ClickValidateMode.None,
            string validationSelector = null,
            int timeout = 15000)
        {
            _client.EnsureTabAlive(_page);
            return ExecuteClickWithValidation(
                clickAction: () =>
                {
                    var download = _page.RunAndWaitForDownloadAsync(() => _locator.ClickAsync(new LocatorClickOptions
                    {
                        Button = button,
                        ClickCount = count,
                        Modifiers = PlaywrightSyncClient.ParseModifiers(modifiers),
                        Force = force
                    })).GetAwaiter().GetResult();
                    string path;
                    if (!string.IsNullOrWhiteSpace(saveAsPath))
                    {
                        var fullPath = Path.GetFullPath(saveAsPath);
                        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? ".");
                        download.SaveAsAsync(fullPath).GetAwaiter().GetResult();
                        path = fullPath;
                    }
                    else
                    {
                        path = download.PathAsync().GetAwaiter().GetResult();
                    }

                    return new DownloadInfo
                    {
                        Url = download.Url,
                        SuggestedFileName = download.SuggestedFilename,
                        SavedPath = path
                    };
                },
                validate: validate,
                validationSelector: validationSelector,
                interval: interval,
                timeout: timeout,
                operationName: "ClickForDownload");
        }

        public void Input(string value, InputMethod inputMethod = InputMethod.Fill, float? typeDelay = null)
        {
            Input(value, inputMethod, typeDelay, false, 500);
        }

        public void Input(
            string value,
            InputMethod inputMethod = InputMethod.Fill,
            float? typeDelay = null,
            bool validateContentAfterInputted = false,
            int interval = 500)
        {
            var expected = value ?? string.Empty;
            if (inputMethod == InputMethod.Fill)
            {
                if (!validateContentAfterInputted)
                {
                    _locator.FillAsync(expected).GetAwaiter().GetResult();
                    return;
                }

                if (interval < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(interval), "interval 不能小于 0。");
                }

                var startedAt = Stopwatch.StartNew();
                while (true)
                {
                    _locator.FillAsync(expected).GetAwaiter().GetResult();
                    if (string.Equals(InputGetValue(), expected, StringComparison.Ordinal))
                    {
                        return;
                    }

                    if (startedAt.ElapsedMilliseconds >= 15000)
                    {
                        throw new TimeoutException("Input timeout after 15000ms: value does not match expected content.");
                    }

                    Thread.Sleep(interval);
                }
            }

            _locator.PressSequentiallyAsync(expected, new LocatorPressSequentiallyOptions
            {
                Delay = typeDelay
            }).GetAwaiter().GetResult();
        }

        public string InputGetValue()
        {
            try
            {
                var value = _locator.InputValueAsync().GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }
            catch
            {
            }

            return _locator.EvaluateAsync<string>("el => (el && 'value' in el) ? el.value : ''")
                .GetAwaiter().GetResult();
        }

        public void Select(string[] values = null, string[] texts = null, int[] indices = null)
        {
            Select(values, texts, indices, false, 500);
        }

        public void Select(
            string[] values = null,
            string[] texts = null,
            int[] indices = null,
            bool validateContentAfterSelected = false,
            int interval = 500)
        {
            var options = new List<SelectOptionValue>();
            if (values != null)
            {
                options.AddRange(values.Where(v => v != null).Select(v => new SelectOptionValue { Value = v }));
            }

            if (texts != null)
            {
                options.AddRange(texts.Where(t => t != null).Select(t => new SelectOptionValue { Label = t }));
            }

            if (indices != null)
            {
                options.AddRange(indices.Select(i => new SelectOptionValue { Index = i }));
            }

            if (options.Count == 0)
            {
                throw new ArgumentException("Select 至少需要 values / texts / indices 之一。");
            }

            if (!validateContentAfterSelected)
            {
                _locator.SelectOptionAsync(options.ToArray()).GetAwaiter().GetResult();
                return;
            }

            if (interval < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(interval), "interval 不能小于 0。");
            }

            var startedAt = Stopwatch.StartNew();
            while (true)
            {
                _locator.SelectOptionAsync(options.ToArray()).GetAwaiter().GetResult();
                if (IsSelectionMatched(values, texts, indices))
                {
                    return;
                }

                if (startedAt.ElapsedMilliseconds >= 15000)
                {
                    throw new TimeoutException("Select timeout after 15000ms: selected options do not match expected content.");
                }

                Thread.Sleep(interval);
            }
        }

        public void Select(params string[] values)
        {
            Select(values: values, texts: null, indices: null, validateContentAfterSelected: false, interval: 500);
        }

        public List<Dictionary<string, object>> SelectGetSelected()
        {
            var json = _locator.EvaluateAsync<string>(
                    "el => JSON.stringify(Array.from((el && el.selectedOptions) || []).map(function(o) {" +
                    "  return {" +
                    "    index: Number(o.index || 0)," +
                    "    text: o.text == null ? '' : String(o.text)," +
                    "    value: o.value == null ? '' : String(o.value)" +
                    "  };" +
                    "}));")
                .GetAwaiter().GetResult();

            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<Dictionary<string, object>>();
            }

            var result = new List<Dictionary<string, object>>();
            using (var doc = JsonDocument.Parse(json))
            {
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    var index = item.TryGetProperty("index", out var indexNode) && indexNode.ValueKind == JsonValueKind.Number
                        ? indexNode.GetInt32()
                        : 0;
                    var text = item.TryGetProperty("text", out var textNode) ? textNode.GetString() ?? string.Empty : string.Empty;
                    var value = item.TryGetProperty("value", out var valueNode) ? valueNode.GetString() ?? string.Empty : string.Empty;

                    result.Add(new Dictionary<string, object>
                    {
                        ["index"] = index,
                        ["text"] = text,
                        ["value"] = value
                    });
                }
            }

            return result;
        }

        public void Check()
        {
            _locator.CheckAsync().GetAwaiter().GetResult();
        }

        public void Uncheck()
        {
            _locator.UncheckAsync().GetAwaiter().GetResult();
        }

        public bool IsChecked()
        {
            return _locator.IsCheckedAsync().GetAwaiter().GetResult();
        }

        public PwElement FindElement(string selector, int index = 0, double? timeout = null, string waitState = null, int delayBefore = 300)
        {
            PlaywrightSyncClient.ApplyDelay(delayBefore);
            var locatorSet = _locator.Locator(selector);
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "index 不能小于 0。");
            }

            var target = locatorSet.Nth(index);
            var parsedState = PlaywrightSyncClient.ParseWaitState(waitState);
            if (parsedState.HasValue || timeout.HasValue)
            {
                target.WaitForAsync(new LocatorWaitForOptions
                {
                    Timeout = timeout.HasValue ? (float?)timeout.Value : null,
                    State = parsedState
                }).GetAwaiter().GetResult();
            }

            var count = locatorSet.CountAsync().GetAwaiter().GetResult();
            if (count <= index)
            {
                throw new InvalidOperationException($"FindElement 未找到匹配元素，selector={selector}, index={index}。");
            }

            return new PwElement(_client, _page, target);
        }

        public PwLocator Locate(string selector, double? timeout = null, int delayBefore = 300)
        {
            _client.EnsureTabAlive(_page);
            if (string.IsNullOrWhiteSpace(selector))
            {
                throw new ArgumentException("selector 不能为空。", nameof(selector));
            }

            PlaywrightSyncClient.ApplyDelay(delayBefore);
            var locator = _locator.Locator(selector);
            if (timeout.HasValue)
            {
                locator.First.WaitForAsync(new LocatorWaitForOptions
                {
                    Timeout = (float)Math.Max(0, timeout.Value),
                    State = WaitForSelectorState.Attached
                }).GetAwaiter().GetResult();
            }

            return new PwLocator(_client, _page, locator);
        }

        public PwElement GetParent(int level = 1)
        {
            if (level <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(level), "level 必须大于 0。");
            }

            ILocator current = _locator;
            for (var i = 0; i < level; i++)
            {
                current = current.Locator("xpath=..").First;
            }

            return new PwElement(_client, _page, current);
        }

        public PwElement[] GetChildren(string selector = null, bool deepdive = false)
        {
            string query;
            if (string.IsNullOrWhiteSpace(selector))
            {
                query = deepdive ? "*" : "xpath=./*";
            }
            else
            {
                query = deepdive ? selector : $":scope > {selector}";
            }

            var children = _locator.Locator(query);
            var count = children.CountAsync().GetAwaiter().GetResult();
            var list = new List<PwElement>(count);
            for (var i = 0; i < count; i++)
            {
                list.Add(new PwElement(_client, _page, children.Nth(i)));
            }

            return list.ToArray();
        }

        public string GetText()
        {
            return _locator.InnerTextAsync().GetAwaiter().GetResult();
        }

        public string GetAttribute(string name)
        {
            return _locator.GetAttributeAsync(name).GetAwaiter().GetResult();
        }

        public void SetAttribute(string name, string value)
        {
            _locator.EvaluateAsync("(element, payload) => element.setAttribute(payload.Name, payload.Value)",
                new AttributePayload { Name = name, Value = value ?? string.Empty })
                .GetAwaiter().GetResult();
        }

        public void TakeScreenshot(string path)
        {
            _locator.ScreenshotAsync(new LocatorScreenshotOptions
            {
                Path = path
            }).GetAwaiter().GetResult();
        }

        public ElementRect GetRect()
        {
            var box = _locator.BoundingBoxAsync().GetAwaiter().GetResult();
            if (box == null)
            {
                return null;
            }

            return new ElementRect
            {
                X = box.X,
                Y = box.Y,
                Width = box.Width,
                Height = box.Height
            };
        }

        public T RunJs<T>(string script, object arg = null, float? timeoutMs = null)
        {
            var options = timeoutMs.HasValue
                ? new LocatorEvaluateOptions { Timeout = timeoutMs.Value }
                : null;
            return _locator.EvaluateAsync<T>(script, arg, options).GetAwaiter().GetResult();
        }

        public void SendKeys(string keys, int? delay = null, float? timeoutMs = null)
        {
            if (string.IsNullOrWhiteSpace(keys))
            {
                throw new ArgumentException("keys 不能为空。", nameof(keys));
            }

            _locator.PressAsync(keys, new LocatorPressOptions
            {
                Delay = delay.HasValue ? (float?)delay.Value : null,
                Timeout = timeoutMs
            }).GetAwaiter().GetResult();
        }

        private string ReadStringWhenTabAlive(Func<string> read)
        {
            if (_page == null || _page.IsClosed)
            {
                return string.Empty;
            }

            _client.EnsureTabAlive(_page);
            return read() ?? string.Empty;
        }

        private sealed class AttributePayload
        {
            public string Name { get; set; }
            public string Value { get; set; }
        }

        private bool IsSelectionMatched(string[] values, string[] texts, int[] indices)
        {
            var selected = SelectGetSelected() ?? new List<Dictionary<string, object>>();
            var selectedValues = new HashSet<string>(
                selected.Where(x => x.ContainsKey("value"))
                    .Select(x => x["value"] == null ? null : x["value"].ToString())
                    .Where(x => x != null),
                StringComparer.Ordinal);
            var selectedTexts = new HashSet<string>(
                selected.Where(x => x.ContainsKey("text"))
                    .Select(x => x["text"] == null ? null : x["text"].ToString())
                    .Where(x => x != null),
                StringComparer.Ordinal);
            var selectedIndices = new HashSet<int>(
                selected.Where(x => x.ContainsKey("index"))
                    .Select(x => Convert.ToInt32(x["index"])));

            var expectedValues = (values ?? new string[0]).Where(x => x != null).ToArray();
            var expectedTexts = (texts ?? new string[0]).Where(x => x != null).ToArray();
            var expectedIndices = indices ?? new int[0];

            return expectedValues.All(selectedValues.Contains) &&
                   expectedTexts.All(selectedTexts.Contains) &&
                   expectedIndices.All(selectedIndices.Contains);
        }

        private bool IsClickValidationSatisfied(ClickValidateMode validate, string validationSelector)
        {
            switch (validate)
            {
                case ClickValidateMode.None:
                    return true;
                case ClickValidateMode.ElementDisappear:
                    return _page.Locator(validationSelector).CountAsync().GetAwaiter().GetResult() == 0;
                case ClickValidateMode.ElementAppear:
                    return _page.Locator(validationSelector).CountAsync().GetAwaiter().GetResult() > 0;
                default:
                    throw new ArgumentOutOfRangeException(nameof(validate), validate, "不支持的点击后校验模式。");
            }
        }

        private T ExecuteClickWithValidation<T>(
            Func<T> clickAction,
            ClickValidateMode validate,
            string validationSelector,
            int interval,
            int timeout,
            string operationName)
        {
            ValidateClickLoopArguments(interval, timeout, validate, validationSelector);
            var startedAt = Stopwatch.StartNew();
            var hasSuccessfulClick = false;
            var latestResult = default(T);
            while (true)
            {
                // 如果上一轮点击已成功，先判断是否已经满足校验，避免不必要的二次点击。
                if (hasSuccessfulClick && IsClickValidationSatisfied(validate, validationSelector))
                {
                    return latestResult;
                }

                try
                {
                    latestResult = clickAction();
                    hasSuccessfulClick = true;
                }
                catch
                {
                    // 点击可能在“目标已变化”的边界时刻抛错；如果此时校验已满足，则视为成功。
                    if (hasSuccessfulClick && IsClickValidationSatisfied(validate, validationSelector))
                    {
                        return latestResult;
                    }

                    throw;
                }

                if (IsClickValidationSatisfied(validate, validationSelector))
                {
                    return latestResult;
                }

                if (startedAt.ElapsedMilliseconds >= timeout)
                {
                    throw new TimeoutException($"{operationName} timeout after {timeout}ms: validation condition not satisfied.");
                }

                Thread.Sleep(interval);
            }
        }

        private static void ValidateClickLoopArguments(int interval, int timeout, ClickValidateMode validate, string validationSelector)
        {
            if (interval < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(interval), "interval 不能小于 0。");
            }

            if (timeout <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), "timeout 必须大于 0。");
            }

            if (validate != ClickValidateMode.None && string.IsNullOrWhiteSpace(validationSelector))
            {
                throw new ArgumentException("validate=ElementDisappear/ElementAppear 时，validationSelector 不能为空。", nameof(validationSelector));
            }
        }

    }

    public sealed class CookieInfo
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public string Domain { get; set; }
        public string Path { get; set; }
        public double Expires { get; set; }
        public bool HttpOnly { get; set; }
        public bool Secure { get; set; }
        public string SameSite { get; set; }
    }

    public sealed class Cookies
    {
        private readonly IReadOnlyList<CookieInfo> _items;

        public Cookies(IReadOnlyList<CookieInfo> items)
        {
            _items = items ?? new List<CookieInfo>();
        }

        public List<Dictionary<string, string>> AsList()
        {
            return _items.Select(c => new Dictionary<string, string>
            {
                ["name"] = c.Name ?? string.Empty,
                ["value"] = c.Value ?? string.Empty,
                ["domain"] = c.Domain ?? string.Empty,
                ["path"] = c.Path ?? string.Empty,
                ["expires"] = c.Expires.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["httpOnly"] = c.HttpOnly.ToString(),
                ["secure"] = c.Secure.ToString(),
                ["sameSite"] = c.SameSite ?? string.Empty
            }).ToList();
        }

        public Dictionary<string, string> AsDict()
        {
            var result = new Dictionary<string, string>();
            foreach (var cookie in _items)
            {
                result[cookie.Name ?? string.Empty] = cookie.Value ?? string.Empty;
            }

            return result;
        }

        public bool ContainsKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            return _items.Any(cookie => string.Equals(cookie.Name, key, StringComparison.Ordinal));
        }

        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            for (var i = _items.Count - 1; i >= 0; i--)
            {
                var cookie = _items[i];
                if (string.Equals(cookie.Name, key, StringComparison.Ordinal))
                {
                    return cookie.Value;
                }
            }

            return null;
        }

        public string AsString()
        {
            return string.Join("; ", _items.Select(c => $"{c.Name}={c.Value}"));
        }
    }

    public sealed class Storages
    {
        private readonly Dictionary<string, string> _items;

        public Storages(Dictionary<string, string> items)
        {
            _items = items ?? new Dictionary<string, string>();
        }

        public List<Dictionary<string, string>> AsList()
        {
            return _items.Select(item => new Dictionary<string, string>
            {
                ["key"] = item.Key ?? string.Empty,
                ["value"] = item.Value ?? string.Empty
            }).ToList();
        }

        public Dictionary<string, string> AsDict()
        {
            return new Dictionary<string, string>(_items);
        }

        public bool ContainsKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            return _items.ContainsKey(key);
        }

        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            return _items.TryGetValue(key, out var value) ? value : null;
        }

        public string AsString()
        {
            return string.Join("; ", _items.Select(item => $"{item.Key}={item.Value}"));
        }
    }

    public sealed class TabInfo
    {
        public string Url { get; set; }
        public string Title { get; set; }
        public bool IsClosed { get; set; }
    }

    public sealed class DownloadInfo
    {
        public string Url { get; set; }
        public string SuggestedFileName { get; set; }
        public string SavedPath { get; set; }
    }

    public sealed class ElementRect
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }
}
