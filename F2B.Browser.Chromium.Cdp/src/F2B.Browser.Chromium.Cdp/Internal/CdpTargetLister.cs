using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal sealed class CdpPageTarget
    {
        public string Id { get; set; }

        public string Title { get; set; }

        public string Url { get; set; }

        public string WebSocketDebuggerUrl { get; set; }
    }

    internal static class CdpTargetLister
    {
        public static IList<CdpPageTarget> ListPageTargets(int port)
        {
            var request = WebRequest.Create(string.Format("http://127.0.0.1:{0}/json/list", port));
            request.Method = "GET";
            request.Timeout = 5000;

            using (var response = request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream))
            {
                var json = reader.ReadToEnd();
                var serializer = new CdpJsonSerializer();
                var items = serializer.DeserializeObject(json) as object[];

                var targets = new List<CdpPageTarget>();
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

                    if (!string.Equals(GetString(dict, "type"), "page", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    targets.Add(new CdpPageTarget
                    {
                        Id = GetString(dict, "id"),
                        Title = GetString(dict, "title"),
                        Url = GetString(dict, "url"),
                        WebSocketDebuggerUrl = GetString(dict, "webSocketDebuggerUrl")
                    });
                }

                return targets;
            }
        }

        private static string GetString(IDictionary<string, object> dict, string key)
        {
            object value;
            return dict.TryGetValue(key, out value) && value != null ? value.ToString() : string.Empty;
        }
    }
}
