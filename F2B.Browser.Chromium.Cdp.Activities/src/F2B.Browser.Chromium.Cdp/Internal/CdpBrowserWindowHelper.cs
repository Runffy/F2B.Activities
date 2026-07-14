using System;
using System.Collections.Generic;
using System.Threading;
using F2B.Browser.Chromium.Cdp.Exceptions;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal static class CdpBrowserWindowHelper
    {
        internal static void Maximize(CdpBrowserConnection connection, string targetId)
        {
            var info = GetWindowInfo(connection, targetId);
            var state = GetWindowState(info);
            if (string.Equals(state, "fullscreen", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(state, "minimized", StringComparison.OrdinalIgnoreCase))
            {
                SetWindowState(connection, info, "normal");
            }

            SetWindowState(connection, info, "maximized");
        }

        internal static void Minimize(CdpBrowserConnection connection, string targetId)
        {
            var info = GetWindowInfo(connection, targetId);
            var state = GetWindowState(info);
            if (string.Equals(state, "fullscreen", StringComparison.OrdinalIgnoreCase))
            {
                SetWindowState(connection, info, "normal");
            }

            SetWindowState(connection, info, "minimized");
        }

        internal static void Normal(CdpBrowserConnection connection, string targetId)
        {
            var info = GetWindowInfo(connection, targetId);
            var state = GetWindowState(info);
            if (string.Equals(state, "fullscreen", StringComparison.OrdinalIgnoreCase))
            {
                SetWindowState(connection, info, "normal");
            }

            SetWindowState(connection, info, "normal");
        }

        internal static void Fullscreen(CdpBrowserConnection connection, string targetId)
        {
            var info = GetWindowInfo(connection, targetId);
            var state = GetWindowState(info);
            if (string.Equals(state, "minimized", StringComparison.OrdinalIgnoreCase))
            {
                SetWindowState(connection, info, "normal");
            }

            SetWindowState(connection, info, "fullscreen");
        }

        private static Dictionary<string, object> GetWindowInfo(CdpBrowserConnection connection, string targetId)
        {
            BrowserException lastError = null;
            for (var attempt = 0; attempt < 50; attempt++)
            {
                try
                {
                    return connection.GetWindowForTarget(targetId);
                }
                catch (BrowserException ex)
                {
                    lastError = ex;
                    Thread.Sleep(20);
                }
            }

            throw lastError ?? new BrowserException("Unable to read browser window information.");
        }

        private static string GetWindowState(Dictionary<string, object> windowInfo)
        {
            var bounds = CdpValueConverter.GetDictionary(windowInfo, "bounds");
            return bounds != null
                ? CdpValueConverter.GetString(bounds, "windowState") ?? "normal"
                : "normal";
        }

        private static void SetWindowState(
            CdpBrowserConnection connection,
            Dictionary<string, object> windowInfo,
            string windowState)
        {
            var windowId = CdpValueConverter.GetInt(windowInfo, "windowId");
            if (windowId <= 0)
            {
                throw new BrowserException("Unable to resolve browser window id.");
            }

            connection.SetWindowBounds(windowId, new Dictionary<string, object>
            {
                { "windowState", windowState }
            });
        }
    }
}
