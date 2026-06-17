using System;
using System.Collections.Generic;

namespace F2B.Browser.Chromium.Bridge
{
    internal static class BridgeTabStatusParser
    {
        public static BridgeTabLoadStatus ParseLoadStatus(string status)
        {
            switch ((status ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "loading":
                    return BridgeTabLoadStatus.Loading;
                case "complete":
                    return BridgeTabLoadStatus.Complete;
                case "unloaded":
                    return BridgeTabLoadStatus.Unloaded;
                default:
                    return BridgeTabLoadStatus.Unknown;
            }
        }

        public static void ApplyTabSnapshot(Dictionary<string, object> data, BwTab tab, BwTabInfo info)
        {
            if (data == null || info == null)
                return;

            var url = BridgeJson.GetString(data, "url") ?? tab?.Url ?? string.Empty;

            info.TabId = BridgeJson.GetInt(data, "tabId", tab != null ? tab.TabId : 0);
            info.WindowId = BridgeJson.GetInt(data, "windowId", tab != null ? tab.WindowId : 0);
            info.Url = url;
            info.Title = BridgeJson.GetString(data, "title") ?? tab?.Title ?? string.Empty;
            info.IsClosed = BridgeJson.GetBool(data, "isClosed");
            info.Active = BridgeJson.GetBool(data, "active", tab != null && tab.Active);
            info.Index = BridgeJson.GetInt(data, "index", tab != null ? tab.Index : 0);
            info.LoadStatusText = BridgeJson.GetString(data, "status");
            info.LoadStatus = ParseLoadStatus(info.LoadStatusText);
            info.IsErrorPage = BridgeJson.GetBool(data, "isErrorPage", BridgeUrlRules.IsErrorPageUrl(url));
            info.IsRestrictedUrl = BridgeJson.GetBool(data, "isRestrictedUrl", BridgeUrlRules.IsRestrictedUrl(url));
            info.IsBlankPage = BridgeJson.GetBool(data, "isBlankPage", BridgeUrlRules.IsBlankUrl(url));
        }

        public static BwBrowserStatus ParseBrowserStatus(
            IBridgeRpcChannel rpc,
            string instanceId,
            BwBrowser browser,
            BridgeRpcResponse response)
        {
            var data = response?.Data;
            var status = new BwBrowserStatus
            {
                BrowserInstanceId = instanceId ?? string.Empty,
                BrowserWindowId = browser != null ? browser.WindowId : 0
            };

            if (data == null || !BridgeJson.GetBool(data, "hasActivatedTab", BridgeJson.GetInt(data, "tabId") > 0))
                return status;

            status.HasActivatedTab = true;
            ApplyTabSnapshot(data, null, status);

            var tabId = status.TabId;
            if (tabId <= 0)
                return status;

            status.ActivatedTab = new BwTab(rpc, instanceId, tabId)
            {
                WindowId = status.WindowId,
                Url = status.Url,
                Title = status.Title,
                Active = status.Active,
                Index = status.Index
            };

            return status;
        }
    }
}
