using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using F2B.Browser.IExplore.Native;

namespace F2B.Browser.IExplore.Com
{
    /// <summary>
    /// Start IE via COM when available; otherwise <c>iexplore.exe</c> (HRESULT 0x800706BA = RPC server unavailable).
    /// </summary>
    public static class IeLauncher
    {
        private const int HresultRpcServerUnavailable = unchecked((int)0x800706BA);

        private static object _applicationRoot;

        public static int Launch(string url, int readyTimeoutMs, out string methodUsed)
        {
            methodUsed = null;

            var progId = Type.GetTypeFromProgID("InternetExplorer.Application");
            if (progId != null)
            {
                try
                {
                    var hwnd = LaunchViaCom(url, readyTimeoutMs);
                    methodUsed = "InternetExplorer.Application";
                    return hwnd;
                }
                catch (COMException ex) when (IsRpcUnavailable(ex))
                {
                    WarmStartIeProcess();
                    try
                    {
                        var hwnd = LaunchViaCom(url, readyTimeoutMs);
                        methodUsed = "InternetExplorer.Application (after warm-start)";
                        return hwnd;
                    }
                    catch (COMException ex2) when (IsRpcUnavailable(ex2))
                    {
                        // fall through to process launch
                    }
                }
            }

            var viaProcess = LaunchViaProcess(url, readyTimeoutMs);
            methodUsed = "iexplore.exe";
            return viaProcess;
        }

        public static int Launch(string url, int readyTimeoutMs = 30000) =>
            Launch(url, readyTimeoutMs, out _);

        /// <summary>
        /// Open IE using <c>iexplore.exe</c> only (no COM). Empty or whitespace <paramref name="url"/> opens <c>about:blank</c>.
        /// Does not wait for the window; use <see cref="EmbeddedIExplore.Connect"/> to attach afterward.
        /// </summary>
        public static void OpenViaIExploreExe(string url)
        {
            StartViaProcess(NormalizeLaunchUrl(url));
        }

        /// <summary>Start navigation only; does not wait for document ready or HWND. Use <see cref="EmbeddedIExplore.Connect"/> to poll.</summary>
        public static void Start(string url, out string methodUsed)
        {
            string method = null;
            StaInvoker.Invoke(() => StartCore(url, out method));
            methodUsed = method;
        }

        /// <summary>
        /// Start IE via COM only on an STA thread (for OpenRPA). Never starts <c>iexplore.exe</c>
        /// (which on Win10/11 often opens Microsoft Edge instead of Trident IE).
        /// </summary>
        public static void StartUsingComOnly(string url, out string methodUsed)
        {
            string method = null;
            StaInvoker.Invoke(() => StartUsingComOnlyCore(url, out method));
            methodUsed = method;
        }

        private static void StartCore(string url, out string methodUsed)
        {
            methodUsed = null;
            var progId = Type.GetTypeFromProgID("InternetExplorer.Application");
            if (progId != null)
            {
                try
                {
                    StartViaCom(NormalizeLaunchUrl(url));
                    methodUsed = "InternetExplorer.Application";
                    return;
                }
                catch (COMException ex) when (IsRpcUnavailable(ex))
                {
                    WarmStartViaCom();
                    try
                    {
                        StartViaCom(NormalizeLaunchUrl(url));
                        methodUsed = "InternetExplorer.Application (after warm-start)";
                        return;
                    }
                    catch (COMException ex2) when (IsRpcUnavailable(ex2))
                    {
                        // fall through
                    }
                }
            }

            StartViaProcess(url);
            methodUsed = "iexplore.exe";
        }

        private static void StartUsingComOnlyCore(string url, out string methodUsed)
        {
            methodUsed = null;
            if (Type.GetTypeFromProgID("InternetExplorer.Application") == null)
            {
                throw new InvalidOperationException(
                    "InternetExplorer.Application is not registered.\n" +
                    GetEnableIeInstructions());
            }

            url = NormalizeLaunchUrl(url);
            try
            {
                StartViaCom(url);
                methodUsed = "InternetExplorer.Application";
                return;
            }
            catch (COMException ex) when (IsRpcUnavailable(ex))
            {
                WarmStartViaCom();
                StartViaCom(url);
                methodUsed = "InternetExplorer.Application (after warm-start)";
            }
        }

        /// <summary>Warm DCOM via COM (avoid iexplore.exe which may launch Edge).</summary>
        private static void WarmStartViaCom()
        {
            try
            {
                StartViaCom("about:blank");
                if (_applicationRoot != null)
                {
                    try
                    {
                        dynamic ie = _applicationRoot;
                        ie.Quit();
                    }
                    catch { /* ignore */ }
                }

                _applicationRoot = null;
                Thread.Sleep(800);
            }
            catch
            {
                // best-effort
            }
        }

        private static void StartViaCom(string url)
        {
            var progId = Type.GetTypeFromProgID("InternetExplorer.Application");
            if (progId == null)
                throw new InvalidOperationException("InternetExplorer.Application is not registered.");

            dynamic ie = Activator.CreateInstance(progId);
            if (ie == null)
                throw new InvalidOperationException("Failed to create InternetExplorer.Application.");

            _applicationRoot = ie;
            ie.Visible = true;
            ie.Navigate2(url);
        }

        private static string NormalizeLaunchUrl(string url) =>
            string.IsNullOrWhiteSpace(url) ? "about:blank" : url.Trim();

        private static void StartViaProcess(string url)
        {
            url = NormalizeLaunchUrl(url);
            var exe = ResolveIExplorePath();
            if (!File.Exists(exe))
            {
                throw new InvalidOperationException(
                    "Internet Explorer was not found at:\n  " + exe + "\n\n" + GetEnableIeInstructions());
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = "\"" + url + "\"",
                UseShellExecute = false
            });
        }

        private static int LaunchViaCom(string url, int readyTimeoutMs)
        {
            var progId = Type.GetTypeFromProgID("InternetExplorer.Application");
            if (progId == null)
                throw new InvalidOperationException("InternetExplorer.Application is not registered.");

            dynamic ie = Activator.CreateInstance(progId);
            if (ie == null)
                throw new InvalidOperationException("Failed to create InternetExplorer.Application.");

            _applicationRoot = ie;
            ie.Visible = true;
            ie.Navigate2(url);

            WaitForReadyState(ie, readyTimeoutMs);

            try { return (int)ie.HWND; }
            catch { return 0; }
        }

        private static int LaunchViaProcess(string url, int readyTimeoutMs)
        {
            url = NormalizeLaunchUrl(url);
            var exe = ResolveIExplorePath();
            if (!File.Exists(exe))
            {
                throw new InvalidOperationException(
                    "Internet Explorer was not found at:\n  " + exe + "\n\n" + GetEnableIeInstructions());
            }

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = "\"" + url + "\"",
                UseShellExecute = false
            };
            Process.Start(psi);

            var hwnd = WaitForIeBrowserWindow(url, "IExplore Test Host", readyTimeoutMs);
            return hwnd;
        }

        /// <summary>Start IE once so the Trident DCOM server may register for this session.</summary>
        private static void WarmStartIeProcess()
        {
            try
            {
                var exe = ResolveIExplorePath();
                if (!File.Exists(exe))
                    return;

                using (var p = Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = "about:blank",
                    UseShellExecute = false
                }))
                {
                    Thread.Sleep(2500);
                    try
                    {
                        if (p != null && !p.HasExited)
                            p.CloseMainWindow();
                    }
                    catch { /* ignore */ }
                }
            }
            catch
            {
                // warm-start is best-effort
            }
        }

        public static int WaitForIeBrowserWindow(string url, string titlePart, int timeoutMs)
        {
            var urlHint = ExtractUrlHint(url);
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                foreach (var browser in ShDocVwHelper.EnumerateShellWindows(internetExplorerOnly: true))
                {
                    if (BrowserMatches(browser.Name, browser.LocationUrl, titlePart, urlHint))
                        return browser.Hwnd;
                }

                IntPtr found = IntPtr.Zero;
                Win32Native.EnumWindows((hwnd, _) =>
                {
                    if (!Win32Native.GetClassNameString(hwnd)
                        .Equals(IeHostWindow.IeFrameClass, StringComparison.OrdinalIgnoreCase))
                        return true;

                    if (!IeHostWindow.IsInternetExplorerBrowser(hwnd))
                        return true;

                    var shell = ShDocVwHelper.FindByHwnd((int)hwnd.ToInt64());
                    var name = shell?.Name ?? Win32Native.GetWindowTextString(hwnd);
                    var loc = shell?.LocationUrl ?? string.Empty;
                    if (BrowserMatches(name, loc, titlePart, urlHint))
                    {
                        found = hwnd;
                        return false;
                    }

                    return true;
                }, IntPtr.Zero);

                if (found != IntPtr.Zero)
                    return (int)found.ToInt64();

                Thread.Sleep(300);
            }

            return 0;
        }

        private static bool BrowserMatches(string name, string locationUrl, string titlePart, string urlHint)
        {
            var titleOk = !string.IsNullOrEmpty(titlePart)
                && (name ?? "").IndexOf(titlePart, StringComparison.OrdinalIgnoreCase) >= 0;
            var urlOk = !string.IsNullOrEmpty(urlHint)
                && (locationUrl ?? "").IndexOf(urlHint, StringComparison.OrdinalIgnoreCase) >= 0;
            return titleOk || urlOk;
        }

        private static string ExtractUrlHint(string url)
        {
            if (string.IsNullOrEmpty(url))
                return string.Empty;
            try
            {
                var uri = new Uri(url);
                var fileName = Path.GetFileName(uri.LocalPath);
                return string.IsNullOrEmpty(fileName) ? uri.AbsolutePath : fileName;
            }
            catch
            {
                return url;
            }
        }

        private static string ResolveIExplorePath()
        {
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "Internet Explorer", "iexplore.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Internet Explorer", "iexplore.exe"),
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path))
                    return path;
            }

            return candidates[0];
        }

        private static void WaitForReadyState(dynamic ie, int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try
                {
                    if ((int)ie.ReadyState == 4)
                        return;
                }
                catch { /* not ready */ }

                Thread.Sleep(200);
            }
        }

        private static bool IsRpcUnavailable(COMException ex) =>
            ex.HResult == HresultRpcServerUnavailable;

        public static string GetEnableIeInstructions() =>
            "Internet Explorer 11 is required for this demo.\n" +
            "1. Open: optionalfeatures.exe (Turn Windows features on or off)\n" +
            "2. Enable: Internet Explorer 11\n" +
            "3. Reboot if prompted, then run this test again.\n" +
            "If COM still returns 0x800706BA, the demo will use iexplore.exe instead.";
    }
}
