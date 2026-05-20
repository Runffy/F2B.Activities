using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using F2B.Browser.IExplore.Com;
using F2B.Browser.IExplore.Native;

namespace IExplore.ComHost
{
    /// <summary>Minimal x86 STA IE launcher for ComHost (COM only, no full plugin DLL).</summary>
    internal static class HostIeLauncher
    {
        private const int HresultRpcServerUnavailable = unchecked((int)0x800706BA);

        private static object _applicationRoot;

        public static void Start(string url, out string methodUsed)
        {
            methodUsed = null;
            if (Type.GetTypeFromProgID("InternetExplorer.Application") == null)
            {
                throw new InvalidOperationException(
                    "InternetExplorer.Application is not registered.\n" + GetEnableIeInstructions());
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

        private static void StartViaCom(string url)
        {
            var progId = Type.GetTypeFromProgID("InternetExplorer.Application");
            dynamic ie = Activator.CreateInstance(progId);
            if (ie == null)
                throw new InvalidOperationException("Failed to create InternetExplorer.Application.");

            _applicationRoot = ie;
            ie.Visible = true;
            ie.Navigate2(url);
        }

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

        private static string NormalizeLaunchUrl(string url) =>
            string.IsNullOrWhiteSpace(url) ? "about:blank" : url.Trim();

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

        private static bool IsRpcUnavailable(COMException ex) =>
            ex.HResult == HresultRpcServerUnavailable;

        private static string GetEnableIeInstructions() =>
            "Internet Explorer 11 is required.\n" +
            "1. Open optionalfeatures.exe and enable Internet Explorer 11\n" +
            "2. Reboot if prompted, then try again.";
    }
}
