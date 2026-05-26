using System;
using System.Collections;
using System.Collections.Generic;
using System.Activities;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace F2B.Basic
{
    /// <summary>
    /// HTTP/S request activity (semantics aligned with Python <c>requests</c>). Only <strong>URL</strong> is configured on the canvas; other inputs are set in the property grid. Returns <see cref="HttpCallResponse"/>.
    /// </summary>
    [Designer(typeof(HttpRequestDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("HTTP Request")]
    public sealed class HttpRequestActivity : CodeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public HttpRequestActivity()
        {
            DisplayName = "HTTP Request";
            Method = "GET";
            Data = new InArgument<string>(string.Empty);
            Timeout = new InArgument<int>(100_000);
            AllowRedirect = new InArgument<bool>(true);
            ThrowOnFailure = new InArgument<bool>(false);
            Verify = new InArgument<bool>(true);
        }

        /// <summary>
        /// HTTP verb (drop-down or free text in the property grid on the right; canvas only shows URL).
        /// </summary>
        [RequiredArgument]
        [DisplayName("Method")]
        [TypeConverter(typeof(HttpVerbChoiceConverter))]
        [DefaultValue("GET")]
        [Category("Input.B")]
        [Description("HTTP verb, e.g. GET, POST, PUT.")]
        public string Method { get; set; }

        [RequiredArgument]
        [DisplayName("URL")]
        [Description("Base URL set on the canvas; query string is merged with Params.")]
        [Category("Input.A")]
        public InArgument<string> Url { get; set; }

        [DisplayName("Headers")]
        [Description(
            "String-to-string map of request headers (like requests.headers). Put Content-Type here to set the entity media type; it is read before send and removed from the headers map to avoid duplicating the content header.")]
        [Category("Input.C")]
        public InArgument<IDictionary<string, string>> Headers { get; set; }

        [DisplayName("Cookies")]
        [Description(
            "String-to-string map written to CookieContainer (like requests cookies).")]
        [Category("Input.D")]
        public InArgument<IDictionary<string, string>> Cookies { get; set; }

        [DisplayName("Params")]
        [Description(
            "Query parameters with object values (like requests.params); merged into the URL query string.")]
        [Category("Input.E")]
        public InArgument<IDictionary<string, object>> Params { get; set; }

        [DisplayName("JSON")]
        [Description(
            "JSON body (like requests json= / dict). When non-empty, serialized as the body and Data is ignored; set Content-Type in Headers (often application/json).")]
        [Category("Input.E")]
        public InArgument<IDictionary<string, object>> Json { get; set; }

        [DisplayName("Data")]
        [Description(
            "Raw body as string (UTF-8), like Python requests data=str, e.g. requests.post(..., data=\"RAW\"). Does not build application/x-www-form-urlencoded; encode the form body yourself, set Content-Type in Headers, and put the encoded string here. Ignored when Json is non-empty.")]
        [Category("Input.E")]
        public InArgument<string> Data { get; set; }

        [DisplayName("Timeout (ms)")]
        [Description("Overall request deadline in milliseconds (cancellation token; aligns with Python requests.timeout when expressed in ms). Default 100000 ms = 100 s.")]
        [Category("Input.Z")]
        public InArgument<int> Timeout { get; set; }

        [DisplayName("Allow redirect")]
        [Description("Whether to follow redirects (allow_redirects).")]
        [Category("Input.F")]
        public InArgument<bool> AllowRedirect { get; set; }

        [DisplayName("Raise on HTTP error")]
        [Description("When true, 4xx/5xx responses still populate Response then throw (raise_for_status).")]
        [Category("Input.F")]
        public InArgument<bool> ThrowOnFailure { get; set; }

        [DisplayName("Verify")]
        [Description(
            "HTTPS server certificate verification. True matches requests.verify=True (default); False matches verify=False — insecure, use only in trusted/dev environments.")]
        [Category("Input.F")]
        public InArgument<bool> Verify { get; set; }

        [DisplayName("Response")]
        [Description(
            "Result: Response.Body.Text (like response.text), Response.Body.Json (typed response.json() as JObject/JArray/JValue), StatusCode, ReasonPhrase, Headers.")]
        [Category("Output")]
        public OutArgument<HttpCallResponse> Response { get; set; }

        private static readonly RemoteCertificateValidationCallback InsecureTlsServerCertificateBypass =
            (sender, certificate, chain, sslPolicyErrors) => true;

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
                Timeout = new InArgument<int>(100_000),
                AllowRedirect = new InArgument<bool>(true),
                ThrowOnFailure = new InArgument<bool>(false),
                Verify = new InArgument<bool>(true),
            };
        }

        /// <remarks>Exposes allowed verbs for designers and converters.</remarks>
        public static IReadOnlyList<string> GetAllowedHttpMethods() => AllowedMethods;

        protected override void Execute(CodeActivityContext context)
        {
            string rawUrl = (Url.Get(context) ?? string.Empty).Trim();
            if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out Uri baseUri))
            {
                throw new ArgumentException("Url must be a valid absolute URI.", nameof(Url));
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

            int timeoutMs = Timeout.Get(context);
            if (timeoutMs <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(Timeout), "Timeout must be a positive number of milliseconds.");
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

            if (!verifyTls && !TrySetServerCertificateBypass(handler))
            {
                throw new NotSupportedException(
                    "Verify cannot be False: this host's HttpClient does not support configuring a certificate callback (typically requires .NET Framework 4.7.1 or later). Set Verify to True or upgrade the runtime to one that exposes ServerCertificateValidationCallback.");
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
                client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;

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

                cts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));

                HttpResponseMessage response;
                try
                {
                    response = client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).GetAwaiter().GetResult();
                }
                catch (OperationCanceledException ex) when (cts.IsCancellationRequested)
                {
                    throw new TimeoutException(
                        $"The request did not complete within {timeoutMs.ToString(CultureInfo.InvariantCulture)} ms.",
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
                            Json = TryParseResponseBodyJson(respBody),
                        },
                    };

                    Response?.Set(context, output);

                    if (throwOnFailure && !response.IsSuccessStatusCode)
                    {
                        string preview = TrimBodyPreview(respBody, 2048);
                        throw new InvalidOperationException(
                            $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}. Body preview: {preview}");
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

        /// <summary>
        /// Writes <see cref="HttpClientHandler.ServerCertificateValidationCallback"/> via reflection (some reference assemblies omit the API at compile time; runtime 4.7.1+ usually supports it).
        /// </summary>
        private static bool TrySetServerCertificateBypass(HttpClientHandler handler)
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

                prop.SetValue(handler, InsecureTlsServerCertificateBypass, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string ResolveRequestMediaType(bool jsonMode, string contentTypeFromHeaders)
        {
            if (!string.IsNullOrWhiteSpace(contentTypeFromHeaders))
            {
                return contentTypeFromHeaders.Trim();
            }

            return jsonMode ? "application/json" : "application/octet-stream";
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
                throw new InvalidOperationException($"Failed to add cookie: {name}", ex);
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
                    throw new InvalidOperationException("Failed to add header: " + name);
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

        /// <remarks>Runtime value may be a non-generic Hashtable or other IDictionary.</remarks>
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
                    "Headers / Cookies must be a Dictionary[String,String] (or compatible non-generic IDictionary).",
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

        /// <remarks>Runtime value may include non-generic Hashtable-based maps.</remarks>
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
                "Params / Json must be a dictionary compatible with IDictionary[string,object].",
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

        private static JToken TryParseResponseBodyJson(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            try
            {
                return JToken.Parse(raw.Trim());
            }
            catch (JsonException)
            {
                return null;
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
                $"Unsupported HTTP method: {raw.Trim()} (choose GET/POST/... in the Method property).",
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
                    throw new ArgumentException("Internal error: unknown Method.", nameof(Method));
            }
        }
    }
}
