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
using System.Net.Security;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;

namespace F2B.Basic
{
    /// <summary>
    /// HTTP/S 请求活动；语义对齐 Python requests。画布上<strong>仅配置 URL</strong>，其余在属性表格中填写；返回 <see cref="HttpCallResponse"/>。
    /// </summary>
    [Designer(typeof(HttpRequestDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("HTTP Request")]
    public sealed class HttpRequestActivity : CodeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public HttpRequestActivity()
        {
            Method = "GET";
            Data = new InArgument<string>(string.Empty);
            TimeoutSeconds = new InArgument<double>(100d);
            AllowRedirect = new InArgument<bool>(true);
            ThrowOnFailure = new InArgument<bool>(false);
            Verify = new InArgument<bool>(true);
        }

        /// <summary>
        /// HTTP 动词（由<strong>右侧属性网格</strong>下拉或手写；画布上仅占位 URL）。
        /// </summary>
        [RequiredArgument]
        [DisplayName("Method")]
        [TypeConverter(typeof(HttpVerbChoiceConverter))]
        [DefaultValue("GET")]
        [Description("HTTP 方法：GET、POST、PUT 等")]
        public string Method { get; set; }

        [RequiredArgument]
        [DisplayName("URL")]
        [Description("基准 URL（画布上配置的唯一下拉项）；Query 可被 Params 合并追加。")]
        public InArgument<string> Url { get; set; }

        [DisplayName("Headers")]
        [Description(
            "string→string 请求头字典，等价 requests.headers。若要对正文指定媒体类型请在字典里写 Content-Type；活动会在发送正文前读出该字段并把它从 Headers 的请求头表中移除以避免重复绑定。")]
        public InArgument<IDictionary<string, string>> Headers { get; set; }

        [DisplayName("Cookies")]
        [Description(
            "string→string 字典写入 CookieContainer，等价 requests 的 cookies")]
        public InArgument<IDictionary<string, string>> Cookies { get; set; }

        [DisplayName("Params")]
        [Description(
            "object 值的查询参数字典（等价 requests.params），合并至 URL Query。")]
        public InArgument<IDictionary<string, object>> Params { get; set; }

        [DisplayName("JSON")]
        [Description(
            "JSON 正文（等价 requests 的 json= / 传入字典）。字典非空时序列化为正文并忽略 Data；通常需在 Headers 中提供 Content-Type（常见为 application/json）。")]
        public InArgument<IDictionary<string, object>> Json { get; set; }

        [DisplayName("Data")]
        [Description(
            "原始请求正文 string（UTF-8），对应 Python requests 的 data=str，例如 requests.post(..., data=\"RAW\")。不会自动拼装 application/x-www-form-urlencoded；" +
            "表单编码请在 Headers 写明 Content-Type 并在本字段放入已编码正文。JSON 非空时本字段忽略。")]
        public InArgument<string> Data { get; set; }

        [DisplayName("Timeout (seconds)")]
        [Description("整体超时（秒）；requests.timeout。")]
        public InArgument<double> TimeoutSeconds { get; set; }

        [DisplayName("Allow redirect")]
        [Description("是否跟随重定向；allow_redirects。")]
        public InArgument<bool> AllowRedirect { get; set; }

        [DisplayName("Raise on HTTP error")]
        [Description("为 true 时 4xx/5xx 在写入 Response 之后再抛异常；等同 raise_for_status。")]
        public InArgument<bool> ThrowOnFailure { get; set; }

        [DisplayName("Verify")]
        [Description(
            "HTTPS 服务端证书校验。True 等价 requests.verify=True（默认）；False 等价 verify=False，存在安全风险，仅用于可信/开发环境")]
        public InArgument<bool> Verify { get; set; }

        [DisplayName("Response")]
        [Description(
            "统一结果：Response.Body.Text（等价 response.text）、Response.Body.Dict（顶层 JSON 对象时等价 response.json()）、StatusCode、ReasonPhrase、Headers。")]
        public OutArgument<HttpCallResponse> Response { get; set; }

        private static readonly JavaScriptSerializer SharedJsonSerializer =
            new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

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
                Data = new InArgument<string>(string.Empty),
                TimeoutSeconds = new InArgument<double>(100d),
                AllowRedirect = new InArgument<bool>(true),
                ThrowOnFailure = new InArgument<bool>(false),
                Verify = new InArgument<bool>(true),
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
            string literalData = Data.Get(context);

            bool useJsonPayload = HasAnyEntry(jsonDict);
            string serializedJson =
                useJsonPayload ? SharedJsonSerializer.Serialize(jsonDict) : null;

            string bodyText =
                useJsonPayload
                    ? serializedJson
                    : (literalData ?? string.Empty);

            double timeoutSec = TimeoutSeconds.Get(context);
            if (timeoutSec <= 0 || double.IsInfinity(timeoutSec) || double.IsNaN(timeoutSec))
            {
                throw new ArgumentOutOfRangeException(nameof(TimeoutSeconds), "超时时间必须为正数秒。");
            }

            bool allowRedirect = AllowRedirect.Get(context);
            bool throwOnFailure = ThrowOnFailure.Get(context);
            bool verifyTls = Verify.Get(context);

            var cookieKv = CoerceStringKeyDictionary(Cookies != null ? Cookies.Get(context) : null);

            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = allowRedirect,
                CookieContainer = cookieKv.Count == 0 ? null : new CookieContainer(),
                UseCookies = true,
            };

            if (!verifyTls && !TryAcceptAllServerCertificates(handler))
            {
                throw new NotSupportedException(
                    "无法将 Verify 设为 False：当前宿主上的 HttpClient 不支持配置证书回调（一般需要 .NET Framework 4.7.1 及以上）。请将 Verify=True 或将目标运行时升级到有 ServerCertificateValidationCallback 的版本。");
            }

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
                    string media = ResolveRequestMediaType(useJsonPayload, contentTypeFromHeaders);

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
                    string respBody =
                        response.Content == null
                            ? string.Empty
                            : response.Content.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty;

                    Dictionary<string, string> respHeadersFolded =
                        MergeHttpHeaderCollections(response.Headers, response.Content?.Headers);

                    var output = new HttpCallResponse
                    {
                        StatusCode = (int)response.StatusCode,
                        ReasonPhrase =
                            response.ReasonPhrase ?? string.Empty,
                        Headers = respHeadersFolded,
                        Body = new HttpCallResponseBody
                        {
                            Text = respBody,
                            Dict = TryParseTopLevelJsonObject(respBody),
                        },
                    };

                    Response?.Set(context, output);

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

        private static string ResolveRequestMediaType(bool jsonMode, string contentTypeFromHeaders)
        {
            if (!string.IsNullOrWhiteSpace(contentTypeFromHeaders))
            {
                return contentTypeFromHeaders.Trim();
            }

            return jsonMode ? "application/json" : "application/octet-stream";
        }

        /// <remarks>通过反射设置 HttpClientHandler.ServerCertificateValidationCallback，以便在运行于带该 CLR API 的机器上时能跳过校验。</remarks>
        private static bool TryAcceptAllServerCertificates(HttpClientHandler handler)
        {
            try
            {
                PropertyInfo prop = typeof(HttpClientHandler).GetProperty(
                    "ServerCertificateValidationCallback",
                    BindingFlags.Public | BindingFlags.Instance);
                if (prop == null || !prop.CanWrite)
                {
                    return false;
                }

                RemoteCertificateValidationCallback bypass =
                    (sender, certificate, chain, sslPolicyErrors) => true;

                prop.SetValue(handler, bypass, null);

                return true;
            }
            catch
            {
                return false;
            }
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
                    UrlEncodeRFC3986(FormatQueryScalarOrJson(item.Value));
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

        private static string FormatQueryScalarOrJson(object value)
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
                return SharedJsonSerializer.Serialize(normalized);
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

        private static Dictionary<string, string> MergeHttpHeaderCollections(
            HttpHeaders main,
            HttpHeaders contentHdrs)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            AppendHeaders(dict, main);
            AppendHeaders(dict, contentHdrs);
            return dict;
        }

        private static void AppendHeaders(Dictionary<string, string> folded, HttpHeaders hdrs)
        {
            if (hdrs == null)
            {
                return;
            }

            foreach (KeyValuePair<string, IEnumerable<string>> pair in hdrs)
            {
                foreach (string slice in pair.Value)
                {
                    FoldHeader(folded, pair.Key, slice ?? string.Empty);
                }
            }
        }

        private static void FoldHeader(Dictionary<string, string> store, string name, string value)
        {
            if (store.TryGetValue(name, out string existing))
            {
                store[name] = string.IsNullOrEmpty(existing) ? value : $"{existing}, {value}";
            }
            else
            {
                store[name] = value ?? string.Empty;
            }
        }

        private static Dictionary<string, object> TryParseTopLevelJsonObject(string raw)
        {
            string t = (raw ?? string.Empty).TrimStart();
            if (t.Length == 0 || t[0] != '{')
            {
                return null;
            }

            try
            {
                object root = SharedJsonSerializer.DeserializeObject(t.Trim());
                if (!(root is IDictionary))
                {
                    return null;
                }

                return NormalizeObjectDictionaryFromJsonRoot(root);
            }
            catch
            {
                return null;
            }
        }

        /// <remarks>DeserializeObject 可能返回 Hashtable / Dictionary&lt;string,object&gt; 混合结构。</remarks>
        private static Dictionary<string, object> NormalizeObjectDictionaryFromJsonRoot(object root)
        {
            var outgoing = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (root == null)
            {
                return outgoing;
            }

            if (root is IDictionary<string, object> direct)
            {
                foreach (KeyValuePair<string, object> kv in direct)
                {
                    if (!string.IsNullOrWhiteSpace(kv.Key))
                    {
                        outgoing[kv.Key.Trim()] = kv.Value;
                    }
                }

                return outgoing;
            }

            if (root is IDictionary opaque)
            {
                foreach (DictionaryEntry e in opaque)
                {
                    string key = Convert.ToString(e.Key, CultureInfo.InvariantCulture)?.Trim();
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        outgoing[key] = e.Value;
                    }
                }

                return outgoing;
            }

            return outgoing;
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
                $"不支持的 HTTP Method：{raw.Trim()}（请在属性的 Method 中选 GET/POST 等）。",
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
