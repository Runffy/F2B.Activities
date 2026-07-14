using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal static class CdpJsonClient
    {
        private const string LocalHost = "127.0.0.1";

        public static IList<CdpTargetInfo> ListTargets(int port)
        {
            return ParseTargets(GetJson(string.Format("http://{0}:{1}/json/list", LocalHost, port)));
        }

        public static CdpBrowserVersionInfo GetBrowserVersion(int port)
        {
            var version = GetJsonObject(string.Format("http://{0}:{1}/json/version", LocalHost, port));
            if (version == null)
            {
                throw new InvalidOperationException(
                    string.Format("Unable to read browser version from http://{0}:{1}/json/version.", LocalHost, port));
            }

            var browserField = GetString(version, "Browser");
            var userAgent = GetString(version, "User-Agent");
            var webSocketDebuggerUrl = GetString(version, "webSocketDebuggerUrl");

            return new CdpBrowserVersionInfo
            {
                Browser = browserField,
                UserAgent = userAgent,
                WebSocketDebuggerUrl = webSocketDebuggerUrl,
                BrowserName = BrowserNameHelper.InferBrowserName(browserField, userAgent)
            };
        }

        public static string GetBrowserWebSocketUrl(int port)
        {
            return GetBrowserVersion(port).WebSocketDebuggerUrl;
        }

        public static string ResolveTargetWebSocketUrl(int port, string targetId)
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                throw new ArgumentNullException("targetId");
            }

            foreach (var target in ListTargets(port))
            {
                if (string.Equals(target.Id, targetId, StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(target.WebSocketDebuggerUrl))
                {
                    return target.WebSocketDebuggerUrl;
                }
            }

            return string.Format("ws://{0}:{1}/devtools/page/{2}", LocalHost, port, targetId);
        }

        private static string GetJson(string url)
        {
            var request = WebRequest.Create(url);
            request.Method = "GET";
            request.Timeout = 10000;

            using (var response = request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        private static Dictionary<string, object> GetJsonObject(string url)
        {
            var serializer = new CdpJsonSerializer();
            return serializer.DeserializeObject(GetJson(url)) as Dictionary<string, object>;
        }

        private static IList<CdpTargetInfo> ParseTargets(string json)
        {
            var serializer = new CdpJsonSerializer();
            var items = serializer.DeserializeObject(json) as object[];
            var targets = new List<CdpTargetInfo>();

            if (items == null)
            {
                return targets;
            }

            foreach (var item in items)
            {
                var dict = item as Dictionary<string, object>;
                if (dict == null)
                {
                    continue;
                }

                targets.Add(new CdpTargetInfo
                {
                    Id = GetString(dict, "id"),
                    Title = GetString(dict, "title"),
                    Url = GetString(dict, "url"),
                    Type = GetString(dict, "type"),
                    WebSocketDebuggerUrl = GetString(dict, "webSocketDebuggerUrl")
                });
            }

            return targets;
        }

        private static string GetString(IDictionary<string, object> dict, string key)
        {
            object value;
            return dict.TryGetValue(key, out value) && value != null ? value.ToString() : string.Empty;
        }
    }
}
