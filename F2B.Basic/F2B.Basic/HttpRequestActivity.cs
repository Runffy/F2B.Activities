using System;
using System.Collections;
using System.Collections.Generic;
using System.Activities;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;

namespace F2B.Basic
{
    /// <summary>
    /// 发起 HTTP/S 调用，语义接近 Python requests。
    /// </summary>
    [Designer(typeof(HttpRequestDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("HTTP Request")]
    public sealed class HttpRequestActivity : CodeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public HttpRequestActivity()
        {
            Method = "GET";
            Body = new InArgument<string>(string.Empty);
            ContentType = new InArgument<string>(string.Empty);
            TimeoutSeconds = new InArgument<double>(100d);
            AllowRedirect = new InArgument<bool>(true);
            ThrowOnFailure = new InArgument<bool>(false);
        }

        /// <summary>HTTP 动词（工具箱中用下拉列表选择）。</summary>
        [RequiredArgument]
        [DisplayName("Method")]
        [Description("HTTP 方法：GET、POST、PUT…（由设计器下拉选择）。")]
        public string Method { get; set; }

        [RequiredArgument]
        [DisplayName("URL")]
        [Description("请求基准地址（绝对 URI）。Query 可被 Params 合并追加。")]
        public InArgument<string> Url { get; set; }

        [DisplayName("Headers")]
        [Description(
            "请求头字典，键值为 string。等价 requests.headers；多条同名键在字典中不可重复，请以一条合并。")]
        public InArgument<IDictionary<string, string>> Headers { get; set; }

        [DisplayName("Cookies")]
        [Description(
            "Cookie 字典写入 CookieContainer（等价 requests 的 cookies）；与手动写 Cookie 头可同时存在，一般不推荐重复。")]
        public InArgument<IDictionary<string, string>> Cookies { get; set; }

        [DisplayName("Params")]
        [Description(
            "查询参数 Dict[str, object]，追加或合并至 URL Query（等价 requests.params）。")]
        public InArgument<IDictionary<string, object>> Params { get; set; }

        [DisplayName("JSON")]
        [Description(
            "JSON 正文 Dict[str, object]，序列化为 application/json（等价 requests(json=…)）。若非空则优先生效，忽略「Body」。")]
        public InArgument<IDictionary<string, object>> Json { get; set; }

        [DisplayName("Body")]
        [Description(
            "原始正文字符串（UTF-8）。在未提供非空 Json 时使用；常用于非 JSON / 手写文本正文。")]
        public InArgument<string> Body { get; set; }

        [DisplayName("Content-Type")]
        [Description(
            "有正文时在 Json/Body 上使用的媒体类型；留空则由 Json→application/json，否则或由 Headers 中 Content-Type（若仍存在）推断。")]
        public InArgument<string> ContentType { get; set; }

        [DisplayName("Timeout (seconds)")]
        [Description("整体超时秒数（requests.timeout）。")]
        public InArgument<double> TimeoutSeconds { get; set; }

        [DisplayName("Allow redirect")]
        [Description("是否跟随重定向（allow_redirects）。")]
        public InArgument<bool> AllowRedirect { get; set; }

        [DisplayName("Raise on HTTP error")]
        [Description("true 时对 4xx/5xx 抛异常（raise_for_status）。")]
        public InArgument<bool> ThrowOnFailure { get; set; }

        [DisplayName("Response body")]
        [Description("响应体（UTF-8）。")]
        public OutArgument<string> ResponseBody { get; set; }

        [DisplayName("Status code")]
        public OutArgument<int> StatusCode { get; set; }

        [DisplayName("Response headers")]
        [Description("响应头文本，每项一行 \"Name: Value\"。")]
        public OutArgument<string> ResponseHeaders { get; set; }

        private static readonly string[] AllowedMethods =
        {
            "GET",
            "POST",
            "PUT",
            "PATCH",
            "DELETE",
            "HEAD",
            "OPTIONS",
        };

        public Activity Create(DependencyObject target)
        {
            return new HttpRequestActivity
            {
                Method = "GET",
                Body = new InArgument<string>(string.Empty),
                ContentType = new InArgument<string>(string.Empty),
                TimeoutSeconds = new InArgument<double>(100d),
                AllowRedirect = new InArgument<bool>(true),
                ThrowOnFailure = new InArgument<bool>(false),
            };
        }

        /// <remarks>用于设计器等调用方枚举合法动词。</remarks>
        public static IReadOnlyList<string> GetAllowedHttpMethods() => AllowedMethods;

        protected override void Execute(CodeActivityContext context)
        {
            string rawUrl = (Url.Get(context) ?? string.Empty).Trim();
            if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out Uri baseUri))
            {
                throw new ArgumentException("Url 必须为有效的绝对 URI。", nameof(Url));
            }

            string methodName = NormalizeMethod(Method);
            HttpMethod httpMethod = ResolveHttpMethod(methodName);

            var headerKv = CoerceStringKeyDictionary(Headers != null ? Headers.Get(context) : null);
            string contentTypeFromHeaders = ExtractRemoveContentTypeInsensitive(headerKv);

            Dictionary<string, object> paramDict = NormalizeObjectDictionary(Params != null ? Params.Get(context) : null);
            Uri uriWithQuery = MergeQueryParameters(baseUri, paramDict);

            Dictionary<string, object> jsonDict = NormalizeObjectDictionary(Json != null ? Json.Get(context) : null);
            string literalBody = Body.Get(context);

            bool useJsonPayload = HasAnyEntry(jsonDict);
            string serializedJson = null;
            if (useJsonPayload)
            {
                serializedJson = new JavaScriptSerializer().Serialize(jsonDict);
            }

            string bodyText =
                useJsonPayload
                    ? serializedJson
                    : (literalBody ?? string.Empty);

            string contentTypeExplicit = ContentType.Get(context);
            contentTypeExplicit =
                string.IsNullOrWhiteSpace(contentTypeExplicit) ? null : contentTypeExplicit.Trim();

            double timeoutSec = TimeoutSeconds.Get(context);
            if (timeoutSec <= 0 || double.IsInfinity(timeoutSec) || double.IsNaN(timeoutSec))
            {
                throw new ArgumentOutOfRangeException(nameof(TimeoutSeconds), "超时时间必须为正数秒。");
            }

            bool allowRedirect = AllowRedirect.Get(context);
            bool throwOnFailure = ThrowOnFailure.Get(context);

            var cookieKv = CoerceStringKeyDictionary(Cookies != null ? Cookies.Get(context) : null);

            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = allowRedirect,
                CookieContainer = cookieKv.Count == 0 ? null : new CookieContainer(),
                UseCookies = true,
            };

            if (handler.CookieContainer != null)
            {
                foreach (KeyValuePair<string, string> c in cookieKv)
                {
                    if (string.IsNullOrWhiteSpace(c.Key))
                    {
                        continue;
                    }

                    TryAddCookie(handler.CookieContainer, uriWithQuery, c.Key.Trim(), c.Value ?? string.Empty);
                }
            }

            using (handler)
            using (var client = new HttpClient(handler, disposeHandler: true))
            using (var cts = new CancellationTokenSource())
            using (var request = new HttpRequestMessage(httpMethod, uriWithQuery))
            {
                client.Timeout = Timeout.InfiniteTimeSpan;

                ApplyHeaderDictionary(headerKv, request);

                if (!request.Headers.Accept.Any())
                {
                    request.Headers.Accept.ParseAdd("*/*");
                }

                if (!TryHeaderExists(request.Headers, "User-Agent"))
                {
                    _ = request.Headers.TryAddWithoutValidation(
                        "User-Agent",
                        "F2B.Basic-HttpRequest/1.0");
                }

                if (!string.IsNullOrEmpty(bodyText))
                {
                    string media = ResolveRequestMediaType(
                        useJsonPayload,
                        contentTypeExplicit,
                        contentTypeFromHeaders);

                    request.Content = new StringContent(bodyText, Encoding.UTF8, media);
                }

                cts.CancelAfter(TimeSpan.FromSeconds(timeoutSec));

                HttpResponseMessage response;
                try
                {
                    response = client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).GetAwaiter().GetResult();
                }
                catch (OperationCanceledException ex) when (cts.IsCancellationRequested)
                {
                    throw new TimeoutException(
                        $"请求在 {timeoutSec.ToString(CultureInfo.InvariantCulture)} 秒内未完成。",
                        ex);
                }

                try
                {
                    StatusCode?.Set(context, (int)response.StatusCode);

                    ResponseHeaders?.Set(context, SerializeHeaders(response.Headers, response.Content?.Headers));

                    string respBody =
                        response.Content == null
                            ? string.Empty
                            : response.Content.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty;

                    ResponseBody?.Set(context, respBody);

                    if (throwOnFailure && !response.IsSuccessStatusCode)
                    {
                        string preview = TrimBodyPreview(respBody, 2048);
                        throw new InvalidOperationException(
                            $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}。正文摘要：{preview}");
                    }
                }
                finally
                {
                    response.Dispose();
                }
            }
        }

        private static bool HasAnyEntry(IReadOnlyDictionary<string, object> dict)
        {
            return dict != null && dict.Count > 0;
        }

        private static string ResolveRequestMediaType(
            bool jsonMode,
            string contentTypeExplicit,
            string contentTypeFromHeaders)
        {
            string media = contentTypeExplicit ?? contentTypeFromHeaders;
            if (!string.IsNullOrWhiteSpace(media))
            {
                return media.Trim();
            }

            if (jsonMode)
            {
                return "application/json";
            }

            return "application/octet-stream";
        }

        private static void TryAddCookie(CookieContainer container, Uri requestUri, string name, string value)
        {
            try
            {
                var ck = new Cookie(name, value ?? string.Empty, "/", requestUri.Host)
                {
                    Secure =
                        string.Equals(requestUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase),
                };

                container.Add(requestUri, ck);
            }
            catch (CookieException ex)
            {
                throw new InvalidOperationException($"无法添加 Cookie：{name}", ex);
            }
        }

        private static Uri MergeQueryParameters(Uri baseUri, IDictionary<string, object> parameters)
        {
            if (parameters == null || parameters.Count == 0)
            {
                return baseUri;
            }

            var ub = new UriBuilder(baseUri);
            var pairs = ParseExistingQueryPairs(ub.Query);

            foreach (KeyValuePair<string, object> item in parameters)
            {
                if (string.IsNullOrEmpty(item.Key))
                {
                    continue;
                }

                string keyEncoded = UrlEncodeRFC3986(item.Key);
                string valueEncoded =
                    UrlEncodeRFC3986(FormatQueryScalarOrJson(item.Value, new JavaScriptSerializer()));
                pairs.Add(new KeyValuePair<string, string>(keyEncoded, valueEncoded));
            }

            ub.Query = BuildQueryFromPairs(pairs);
            return ub.Uri;
        }

        private static string BuildQueryFromPairs(List<KeyValuePair<string, string>> pairs)
        {
            if (pairs.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(
                "&",
                pairs.Select(p => p.Key + "=" + p.Value).ToArray());
        }

        private static List<KeyValuePair<string, string>> ParseExistingQueryPairs(string query)
        {
            var list = new List<KeyValuePair<string, string>>();
            if (string.IsNullOrEmpty(query))
            {
                return list;
            }

            string q = query.StartsWith("?", StringComparison.Ordinal) ? query.Substring(1) : query;
            if (string.IsNullOrWhiteSpace(q))
            {
                return list;
            }

            foreach (string segment in q.Split('&'))
            {
                if (string.IsNullOrWhiteSpace(segment))
                {
                    continue;
                }

                int eq = segment.IndexOf('=');
                if (eq < 0)
                {
                    string kOnly = UrlEncodeRFC3986(Uri.UnescapeDataString(segment));
                    list.Add(new KeyValuePair<string, string>(kOnly, string.Empty));
                }
                else
                {
                    string k = Uri.UnescapeDataString(segment.Substring(0, eq));
                    string v = Uri.UnescapeDataString(segment.Substring(eq + 1));
                    list.Add(new KeyValuePair<string, string>(
                        UrlEncodeRFC3986(k),
                        UrlEncodeRFC3986(v)));
                }
            }

            return list;
        }

        private static string FormatQueryScalarOrJson(object value, JavaScriptSerializer json)
        {
            if (value == null || Convert.IsDBNull(value))
            {
                return string.Empty;
            }

            var s = value as string;
            if (s != null)
            {
                return s;
            }

            if (value is char)
            {
                return value.ToString();
            }

            if (value is bool b)
            {
                return b ? "True" : "False";
            }

            switch (value)
            {
                case byte v:
                    return v.ToString(CultureInfo.InvariantCulture);
                case sbyte v:
                    return v.ToString(CultureInfo.InvariantCulture);
                case short v:
                    return v.ToString(CultureInfo.InvariantCulture);
                case ushort v:
                    return v.ToString(CultureInfo.InvariantCulture);
                case int v:
                    return v.ToString(CultureInfo.InvariantCulture);
                case uint v:
                    return v.ToString(CultureInfo.InvariantCulture);
                case long v:
                    return v.ToString(CultureInfo.InvariantCulture);
                case ulong v:
                    return v.ToString(CultureInfo.InvariantCulture);
                case float v:
                    return v.ToString(CultureInfo.InvariantCulture);
                case double v:
                    return v.ToString(CultureInfo.InvariantCulture);
                case decimal v:
                    return v.ToString(CultureInfo.InvariantCulture);
                case DateTime v:
                    return v.ToString("o", CultureInfo.InvariantCulture);
                case Enum e:
                    return Convert.ToString(e, CultureInfo.InvariantCulture);
                case Guid v:
                    return v.ToString("D");
            }

            if (IsDictionaryLike(value))
            {
                Dictionary<string, object> normalized = NormalizeObjectDictionary(value);
                return json.Serialize(normalized);
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static string UrlEncodeRFC3986(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            byte[] utf8Bytes = Encoding.UTF8.GetBytes(text);

            var sb = new StringBuilder();
            foreach (byte b in utf8Bytes)
            {
                bool unreserved =
                    (b >= 0x41 && b <= 0x5A) ||
                    (b >= 0x61 && b <= 0x7A) ||
                    (b >= 0x30 && b <= 0x39) ||
                    b == 0x2D ||
                    b == 0x2E ||
                    b == 0x5F ||
                    b == 0x7E;

                if (unreserved)
                {
                    sb.Append((char)b);
                }
                else
                {
                    sb.Append('%').Append(b.ToString("X2"));
                }
            }

            return sb.ToString();
        }

        private static void ApplyHeaderDictionary(IDictionary<string, string> kv, HttpRequestMessage request)
        {
            foreach (KeyValuePair<string, string> pair in kv.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
            {
                string name = pair.Key;
                if (string.Equals(name, "Content-Type", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "Content-Encoding", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!request.Headers.TryAddWithoutValidation(name, pair.Value ?? string.Empty))
                {
                    throw new InvalidOperationException("无法添加请求头：" + name);
                }
            }
        }

        private static string ExtractRemoveContentTypeInsensitive(IDictionary<string, string> headers)
        {
            string ct = null;
            var matches = headers.Keys.Where(
                    k =>
                        string.Equals(k, "Content-Type", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (string k in matches)
            {
                if (ct == null)
                {
                    ct = headers[k];
                }

                headers.Remove(k);
            }

            return string.IsNullOrWhiteSpace(ct) ? null : ct.Trim();
        }

        /// <remarks>运行时可能为 Hashtable 等非泛型 IDictionary。</remarks>
        private static Dictionary<string, string> CoerceStringKeyDictionary(object raw)
        {
            if (raw == null)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var typed = raw as IDictionary<string, string>;
            if (typed != null)
            {
                var dst = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, string> p in typed)
                {
                    if (!string.IsNullOrWhiteSpace(p.Key))
                    {
                        dst[p.Key.Trim()] = p.Value ?? string.Empty;
                    }
                }

                return dst;
            }

            var dyn = raw as IDictionary;
            if (dyn == null)
            {
                throw new ArgumentException(
                    "Headers / Cookies 应为 Dictionary[String,String]（或等价非泛型 IDictionary）实例。",
                    nameof(raw));
            }

            var dst2 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in dyn)
            {
                string k = Convert.ToString(entry.Key, CultureInfo.InvariantCulture)?.Trim();
                if (string.IsNullOrEmpty(k))
                {
                    continue;
                }

                dst2[k] =
                    Convert.ToString(entry.Value, CultureInfo.InvariantCulture)
                    ?? string.Empty;
            }

            return dst2;
        }

        /// <remarks>运行时可能为非泛型 Hashtable 等。</remarks>
        private static Dictionary<string, object> NormalizeObjectDictionary(object rawRoot)
        {
            var merged = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (rawRoot == null)
            {
                return merged;
            }

            if (rawRoot is IDictionary<string, object> sos)
            {
                foreach (KeyValuePair<string, object> kv in sos)
                {
                    if (!string.IsNullOrWhiteSpace(kv.Key))
                    {
                        merged[kv.Key.Trim()] = kv.Value;
                    }
                }

                return merged;
            }

            IDictionary dyn = rawRoot as IDictionary;
            if (dyn != null)
            {
                foreach (DictionaryEntry e in dyn)
                {
                    string k = Convert.ToString(e.Key, CultureInfo.InvariantCulture)?.Trim();
                    if (string.IsNullOrWhiteSpace(k))
                    {
                        continue;
                    }

                    merged[k] = e.Value;
                }

                return merged;
            }

            throw new ArgumentException(
                "Params / Json 需为兼容 IDictionary[string,object] 的字典实例。",
                nameof(rawRoot));
        }

        private static bool IsDictionaryLike(object value)
        {
            return value is IDictionary<string, object> || value is IDictionary;
        }

        private static bool TryHeaderExists(HttpRequestHeaders headers, string name)
        {
            IEnumerable<string> seq;
            return headers != null &&
                   headers.TryGetValues(name, out seq) &&
                   seq != null &&
                   seq.Any();
        }

        private static string SerializeHeaders(HttpHeaders responseHeaders, HttpHeaders contentHeaders)
        {
            var sb = new StringBuilder();
            if (responseHeaders != null)
            {
                SerializeOneHeaderCollection(sb, responseHeaders);
            }

            if (contentHeaders != null)
            {
                SerializeOneHeaderCollection(sb, contentHeaders);
            }

            return sb.Length == 0 ? string.Empty : sb.ToString().TrimEnd('\n');
        }

        private static void SerializeOneHeaderCollection(StringBuilder sb, HttpHeaders headers)
        {
            foreach (KeyValuePair<string, IEnumerable<string>> pair in headers)
            {
                foreach (string value in pair.Value)
                {
                    sb.Append(pair.Key).Append(": ").Append(value).Append('\n');
                }
            }
        }

        private static string TrimBodyPreview(string body, int maxLen)
        {
            if (string.IsNullOrEmpty(body))
            {
                return "(empty)";
            }

            string escaped = body.Replace("\r", "\\r").Replace("\n", "\\n");
            if (escaped.Length <= maxLen)
            {
                return escaped;
            }

            return escaped.Substring(0, maxLen) + "...";
        }

        private static string NormalizeMethod(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return "GET";
            }

            string m = raw.Trim().ToUpperInvariant();
            if (AllowedMethods.Contains(m))
            {
                return m;
            }

            throw new ArgumentException(
                $"不支持的 HTTP Method：{raw.Trim()}（请从下拉列表中选择）",
                nameof(Method));
        }

        private static HttpMethod ResolveHttpMethod(string normalizedVerb)
        {
            switch (normalizedVerb)
            {
                case "GET":
                    return HttpMethod.Get;
                case "POST":
                    return HttpMethod.Post;
                case "PUT":
                    return HttpMethod.Put;
                case "PATCH":
                    return new HttpMethod("PATCH");
                case "DELETE":
                    return HttpMethod.Delete;
                case "HEAD":
                    return HttpMethod.Head;
                case "OPTIONS":
                    return HttpMethod.Options;
                default:
                    throw new ArgumentException("内部错误：未知的 Method。", nameof(Method));
            }
        }
    }
}
