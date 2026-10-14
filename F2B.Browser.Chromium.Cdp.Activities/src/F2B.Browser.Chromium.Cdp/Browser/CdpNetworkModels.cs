using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using F2B.Browser.Chromium.Cdp.Internal;

namespace F2B.Browser.Chromium.Cdp.Browser
{
    public sealed class CdpNetworkPacket
    {
        private readonly object _extraLock = new object();
        private CdpNetworkRequest _request;
        private CdpNetworkResponse _response;
        private CdpNetworkFailInfo _failInfo;

        internal CdpNetworkPacket(string tabId, string target)
        {
            TabId = tabId ?? string.Empty;
            Target = target;
        }

        public string TabId { get; private set; }

        /// <summary>Matched URL pattern, or null when listening to all URLs.</summary>
        public string Target { get; private set; }

        public bool IsFailed { get; internal set; }

        public string Url
        {
            get { return Request.Url; }
        }

        public string Method
        {
            get { return Request.Method; }
        }

        public string FrameId { get; internal set; }

        public string ResourceType { get; internal set; }

        public CdpNetworkRequest Request
        {
            get
            {
                if (_request == null)
                {
                    _request = new CdpNetworkRequest(this, RawRequest, RawPostData);
                }

                return _request;
            }
        }

        public CdpNetworkResponse Response
        {
            get
            {
                if (_response == null)
                {
                    _response = new CdpNetworkResponse(this, RawResponse, RawBody, Base64Body);
                }

                return _response;
            }
        }

        public CdpNetworkFailInfo FailInfo
        {
            get
            {
                if (_failInfo == null)
                {
                    _failInfo = new CdpNetworkFailInfo(RawFailInfo);
                }

                return _failInfo;
            }
        }

        internal Dictionary<string, object> RawRequest { get; set; }

        internal string RawPostData { get; set; }

        internal Dictionary<string, object> RawResponse { get; set; }

        internal string RawBody { get; set; }

        internal bool Base64Body { get; set; }

        internal Dictionary<string, object> RawFailInfo { get; set; }

        internal Dictionary<string, object> RequestExtraInfo { get; set; }

        internal Dictionary<string, object> ResponseExtraInfo { get; set; }

        public bool WaitExtraInfo(int timeoutMs = 0, bool throwOnTimeout = true)
        {
            if (timeoutMs <= 0)
            {
                while (true)
                {
                    lock (_extraLock)
                    {
                        if (ResponseExtraInfo != null)
                        {
                            return true;
                        }
                    }

                    System.Threading.Thread.Sleep(10);
                }
            }

            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                lock (_extraLock)
                {
                    if (ResponseExtraInfo != null)
                    {
                        return true;
                    }
                }

                System.Threading.Thread.Sleep(10);
            }

            if (throwOnTimeout)
            {
                throw new Exceptions.BrowserException(
                    string.Format("Timed out after {0} ms waiting for extra info.", timeoutMs));
            }

            return false;
        }

        internal void SetResponseExtraInfo(Dictionary<string, object> extra)
        {
            lock (_extraLock)
            {
                ResponseExtraInfo = extra;
            }
        }

        internal void SetRequestExtraInfo(Dictionary<string, object> extra)
        {
            lock (_extraLock)
            {
                RequestExtraInfo = extra;
            }
        }

        public override string ToString()
        {
            var target = Target == null ? "True" : "\"" + Target + "\"";
            return string.Format("CdpNetworkPacket target={0} url=\"{1}\"", target, Url);
        }
    }

    public sealed class CdpNetworkRequest
    {
        private readonly CdpNetworkPacket _packet;
        private readonly Dictionary<string, object> _request;
        private readonly string _rawPostData;
        private Dictionary<string, string> _headers;
        private object _postDataJson;
        private bool _postDataJsonResolved;
        private IList<CdpCookieItem> _cookies;

        internal CdpNetworkRequest(
            CdpNetworkPacket packet,
            Dictionary<string, object> rawRequest,
            string rawPostData)
        {
            _packet = packet;
            _request = CdpValueConverter.GetDictionary(rawRequest, "request") ?? rawRequest ?? new Dictionary<string, object>();
            _rawPostData = rawPostData;
        }

        public string Url
        {
            get { return CdpValueConverter.GetString(_request, "url") ?? string.Empty; }
        }

        public string Method
        {
            get { return CdpValueConverter.GetString(_request, "method") ?? string.Empty; }
        }

        public IDictionary<string, string> Headers
        {
            get
            {
                if (_headers == null)
                {
                    _headers = CdpNetworkModelHelper.MergeHeaders(
                        CdpValueConverter.GetDictionary(_request, "headers"),
                        ExtraInfo.Headers);
                }

                return _headers;
            }
        }

        public IDictionary<string, string> Params
        {
            get { return CdpNetworkModelHelper.ParseQuery(Url); }
        }

        public string PostDataText
        {
            get
            {
                if (!string.IsNullOrEmpty(_rawPostData))
                {
                    return _rawPostData;
                }

                return CdpValueConverter.GetString(_request, "postData");
            }
        }

        public object PostDataJson
        {
            get
            {
                if (!_postDataJsonResolved)
                {
                    _postDataJsonResolved = true;
                    _postDataJson = CdpNetworkModelHelper.TryParseJson(PostDataText);
                }

                return _postDataJson;
            }
        }

        public IList<CdpCookieItem> Cookies
        {
            get
            {
                if (_cookies == null)
                {
                    _cookies = CdpNetworkModelHelper.ReadCookies(ExtraInfo.AssociatedCookies);
                }

                return _cookies;
            }
        }

        public CdpNetworkRequestExtraInfo ExtraInfo
        {
            get { return new CdpNetworkRequestExtraInfo(_packet.RequestExtraInfo); }
        }

        public IDictionary<string, object> Raw
        {
            get { return _request; }
        }
    }

    public sealed class CdpNetworkResponse
    {
        private readonly CdpNetworkPacket _packet;
        private readonly Dictionary<string, object> _response;
        private readonly string _rawBody;
        private readonly bool _isBase64;
        private Dictionary<string, string> _headers;
        private byte[] _bodyBytes;
        private object _bodyJson;
        private bool _bodyJsonResolved;

        internal CdpNetworkResponse(
            CdpNetworkPacket packet,
            Dictionary<string, object> rawResponse,
            string rawBody,
            bool isBase64)
        {
            _packet = packet;
            _response = rawResponse ?? new Dictionary<string, object>();
            _rawBody = rawBody ?? string.Empty;
            _isBase64 = isBase64;
        }

        public string Url
        {
            get { return CdpValueConverter.GetString(_response, "url") ?? string.Empty; }
        }

        public int Status
        {
            get { return CdpValueConverter.GetInt(_response, "status"); }
        }

        public string StatusText
        {
            get { return CdpValueConverter.GetString(_response, "statusText") ?? string.Empty; }
        }

        public string MimeType
        {
            get { return CdpValueConverter.GetString(_response, "mimeType") ?? string.Empty; }
        }

        public IDictionary<string, string> Headers
        {
            get
            {
                if (_headers == null)
                {
                    _headers = CdpNetworkModelHelper.MergeHeaders(
                        CdpValueConverter.GetDictionary(_response, "headers"),
                        ExtraInfo.Headers);
                }

                return _headers;
            }
        }

        public string BodyText
        {
            get { return _rawBody; }
        }

        public byte[] BodyBytes
        {
            get
            {
                if (_bodyBytes == null)
                {
                    if (_isBase64)
                    {
                        try
                        {
                            _bodyBytes = Convert.FromBase64String(_rawBody ?? string.Empty);
                        }
                        catch
                        {
                            _bodyBytes = new byte[0];
                        }
                    }
                    else
                    {
                        _bodyBytes = Encoding.UTF8.GetBytes(_rawBody ?? string.Empty);
                    }
                }

                return _bodyBytes;
            }
        }

        public object BodyJson
        {
            get
            {
                if (!_bodyJsonResolved)
                {
                    _bodyJsonResolved = true;
                    _bodyJson = _isBase64 ? null : CdpNetworkModelHelper.TryParseJson(_rawBody);
                }

                return _bodyJson;
            }
        }

        public CdpNetworkResponseExtraInfo ExtraInfo
        {
            get { return new CdpNetworkResponseExtraInfo(_packet.ResponseExtraInfo); }
        }

        public IDictionary<string, object> Raw
        {
            get { return _response; }
        }
    }

    public class CdpNetworkExtraInfo
    {
        internal CdpNetworkExtraInfo(Dictionary<string, object> extraInfo)
        {
            AllInfo = extraInfo ?? new Dictionary<string, object>();
        }

        public IDictionary<string, object> AllInfo { get; private set; }

        public IDictionary<string, string> Headers
        {
            get
            {
                return CdpValueConverter.ToStringDictionary(
                    CdpValueConverter.GetDictionary(AllInfo as Dictionary<string, object>, "headers"));
            }
        }

        public string RequestId
        {
            get { return CdpValueConverter.GetString(AllInfo as Dictionary<string, object>, "requestId"); }
        }
    }

    public sealed class CdpNetworkRequestExtraInfo : CdpNetworkExtraInfo
    {
        internal CdpNetworkRequestExtraInfo(Dictionary<string, object> extraInfo)
            : base(extraInfo)
        {
        }

        public IList AssociatedCookies
        {
            get { return CdpValueConverter.GetList(AllInfo as Dictionary<string, object>, "associatedCookies"); }
        }
    }

    public sealed class CdpNetworkResponseExtraInfo : CdpNetworkExtraInfo
    {
        internal CdpNetworkResponseExtraInfo(Dictionary<string, object> extraInfo)
            : base(extraInfo)
        {
        }

        public int StatusCode
        {
            get { return CdpValueConverter.GetInt(AllInfo as Dictionary<string, object>, "statusCode"); }
        }
    }

    public sealed class CdpNetworkFailInfo
    {
        internal CdpNetworkFailInfo(Dictionary<string, object> failInfo)
        {
            Raw = failInfo ?? new Dictionary<string, object>();
        }

        public string ErrorText
        {
            get { return CdpValueConverter.GetString(Raw as Dictionary<string, object>, "errorText"); }
        }

        public bool Canceled
        {
            get { return CdpValueConverter.GetBool(Raw as Dictionary<string, object>, "canceled"); }
        }

        public string BlockedReason
        {
            get { return CdpValueConverter.GetString(Raw as Dictionary<string, object>, "blockedReason"); }
        }

        public object CorsErrorStatus
        {
            get
            {
                var dict = Raw as Dictionary<string, object>;
                if (dict == null)
                {
                    return null;
                }

                object value;
                return dict.TryGetValue("corsErrorStatus", out value) ? value : null;
            }
        }

        public IDictionary<string, object> Raw { get; private set; }
    }

    internal static partial class CdpNetworkModelHelper
    {
        internal static Dictionary<string, string> MergeHeaders(
            Dictionary<string, object> primary,
            IDictionary<string, string> extra)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in ToStringDictionary(primary))
            {
                result[pair.Key] = pair.Value;
            }

            if (extra != null)
            {
                foreach (var pair in extra)
                {
                    if (!result.ContainsKey(pair.Key))
                    {
                        result[pair.Key] = pair.Value;
                    }
                }
            }

            return result;
        }

        internal static Dictionary<string, string> ToStringDictionary(Dictionary<string, object> source)
        {
            return CdpValueConverter.ToStringDictionary(source);
        }

        internal static object TryParseJson(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var trimmed = text.TrimStart();
            if (trimmed.Length == 0)
            {
                return null;
            }

            var first = trimmed[0];
            if (first != '{' && first != '[')
            {
                return null;
            }

            try
            {
                return new CdpJsonSerializer().DeserializeObject(text);
            }
            catch
            {
                return null;
            }
        }

        internal static Dictionary<string, string> ParseQuery(string url)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(url))
            {
                return result;
            }

            var queryIndex = url.IndexOf('?');
            if (queryIndex < 0 || queryIndex >= url.Length - 1)
            {
                return result;
            }

            var query = url.Substring(queryIndex + 1);
            var hash = query.IndexOf('#');
            if (hash >= 0)
            {
                query = query.Substring(0, hash);
            }

            foreach (var part in query.Split('&'))
            {
                if (string.IsNullOrEmpty(part))
                {
                    continue;
                }

                var eq = part.IndexOf('=');
                string key;
                string value;
                if (eq < 0)
                {
                    key = Uri.UnescapeDataString(part.Replace('+', ' '));
                    value = string.Empty;
                }
                else
                {
                    key = Uri.UnescapeDataString(part.Substring(0, eq).Replace('+', ' '));
                    value = Uri.UnescapeDataString(part.Substring(eq + 1).Replace('+', ' '));
                }

                result[key] = value;
            }

            return result;
        }

        internal static IList<CdpCookieItem> ReadCookies(IList associatedCookies)
        {
            var items = new List<CdpCookieItem>();
            if (associatedCookies == null)
            {
                return items;
            }

            foreach (var entry in associatedCookies)
            {
                var row = entry as Dictionary<string, object>;
                if (row == null)
                {
                    continue;
                }

                var blocked = CdpValueConverter.GetList(row, "blockedReasons");
                if (blocked != null && blocked.Count > 0)
                {
                    continue;
                }

                var cookie = CdpValueConverter.GetDictionary(row, "cookie");
                var name = CdpValueConverter.GetString(cookie, "name");
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                items.Add(new CdpCookieItem(name, CdpValueConverter.GetString(cookie, "value") ?? string.Empty));
            }

            return items;
        }
    }
}
