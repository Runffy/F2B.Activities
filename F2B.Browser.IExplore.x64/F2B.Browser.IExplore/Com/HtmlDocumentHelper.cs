using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using F2B.Browser.IExplore.Native;

namespace F2B.Browser.IExplore.Com
{
    internal static class HtmlDocumentHelper
    {
        private static uint? _wmHtmlGetObject;

        private static uint WmHtmlGetObject =>
            _wmHtmlGetObject ?? (_wmHtmlGetObject = Win32Native.RegisterWindowMessage("WM_HTML_GETOBJECT")).Value;

        public const string IeServerClassName = "Internet Explorer_Server";

        public static IntPtr FindInternetExplorerServer(IntPtr root)
        {
            if (Win32Native.GetClassNameString(root) == IeServerClassName)
                return root;

            IntPtr found = IntPtr.Zero;
            Win32Native.EnumChildWindows(root, (child, _) =>
            {
                if (Win32Native.GetClassNameString(child) == IeServerClassName)
                {
                    found = child;
                    return false;
                }

                var nested = FindInternetExplorerServer(child);
                if (nested != IntPtr.Zero)
                {
                    found = nested;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        /// <summary>Collect every <c>Internet Explorer_Server</c> under <paramref name="root"/> (depth-first).</summary>
        public static IList<IntPtr> CollectInternetExplorerServers(IntPtr root)
        {
            var list = new List<IntPtr>();
            CollectInternetExplorerServersCore(root, list);
            return list;
        }

        private static void CollectInternetExplorerServersCore(IntPtr hwnd, IList<IntPtr> list)
        {
            if (hwnd == IntPtr.Zero || !Win32Native.IsWindow(hwnd))
                return;

            if (Win32Native.GetClassNameString(hwnd) == IeServerClassName)
                list.Add(hwnd);

            Win32Native.EnumChildWindows(hwnd, (child, _) =>
            {
                CollectInternetExplorerServersCore(child, list);
                return true;
            }, IntPtr.Zero);
        }

        public static IHTMLDocument2 GetDocumentFromIeServer(IntPtr ieServerHwnd)
        {
            IntPtr lResult;
            var sent = Win32Native.SendMessageTimeout(
                ieServerHwnd,
                WmHtmlGetObject,
                IntPtr.Zero,
                IntPtr.Zero,
                Win32Native.SmtoAbortIfHung,
                5000,
                out lResult);

            if (sent == IntPtr.Zero || lResult == IntPtr.Zero)
                throw new InvalidOperationException(
                    "WM_HTML_GETOBJECT failed for hwnd=" + ieServerHwnd + ", err=" + Marshal.GetLastWin32Error());

            Guid iid = MsHtmlGuids.IID_IHTMLDocument2;
            object docObj;
            int hr = Win32Native.ObjectFromLresult(lResult, ref iid, 0, out docObj);
            if (hr != 0 || docObj == null)
                throw new InvalidOperationException("ObjectFromLresult failed, hr=0x" + hr.ToString("X"));

            var doc = docObj as IHTMLDocument2;
            if (doc == null)
                throw new InvalidOperationException("Object is not IHTMLDocument2.");

            return doc;
        }

        public static string ReadOuterHtml(IHTMLDocument2 doc)
        {
            if (doc == null) return string.Empty;
            try
            {
                dynamic d = doc;
                dynamic html = d.documentElement;
                if (html != null)
                    return (string)html.outerHTML ?? string.Empty;
            }
            catch { /* fall through */ }

            try
            {
                dynamic body = doc.body;
                if (body != null)
                    return (string)body.outerHTML ?? string.Empty;
            }
            catch { /* ignore */ }

            return string.Empty;
        }

        public static string ReadDocumentTitle(IHTMLDocument2 doc)
        {
            if (doc == null) return string.Empty;
            return ComSafe.ReadString(() =>
            {
                dynamic d = doc;
                return (string)d.title;
            });
        }

        public static string ReadDocumentUrl(IHTMLDocument2 doc)
        {
            if (doc == null) return string.Empty;
            return ComSafe.ReadString(() =>
            {
                dynamic d = doc;
                return (string)d.url;
            });
        }

        public static string ReadReadyState(IHTMLDocument2 doc)
        {
            if (doc == null) return string.Empty;
            return ComSafe.ReadString(() =>
            {
                dynamic d = doc;
                return (string)d.readyState;
            });
        }

        /// <summary>True when MSHTML reports a navigable state (not uninitialized).</summary>
        public static bool IsDocumentReadable(IHTMLDocument2 doc)
        {
            if (doc == null) return false;
            var state = ReadReadyState(doc);
            if (string.IsNullOrEmpty(state))
                return true;
            return state.Equals("complete", StringComparison.OrdinalIgnoreCase)
                || state.Equals("interactive", StringComparison.OrdinalIgnoreCase)
                || state.Equals("loading", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Returns null if this HWND is not a Trident host.</summary>
        public static bool TryGetDocument(IntPtr topLevelHwnd, out IntPtr ieServerHwnd, out IHTMLDocument2 document)
        {
            ieServerHwnd = IntPtr.Zero;
            document = null;
            if (topLevelHwnd == IntPtr.Zero || !Win32Native.IsWindow(topLevelHwnd))
                return false;

            ieServerHwnd = FindInternetExplorerServer(topLevelHwnd);
            if (ieServerHwnd == IntPtr.Zero)
                return false;

            try
            {
                document = GetDocumentFromIeServer(ieServerHwnd);
                return document != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
