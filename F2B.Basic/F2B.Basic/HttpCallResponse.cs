using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace F2B.Basic
{
    /// <summary>
    /// HTTP response body carrier: <see cref="HttpCallResponseBody.Text"/> mirrors <c>response.text</c> (UTF-8 decoded string);
    /// <see cref="HttpCallResponseBody.Json"/> is the parsed payload as <see cref="JToken"/> (e.g. <see cref="JObject"/>,
    /// <see cref="JArray"/>), or null if parsing fails or the body is not JSON.
    /// </summary>
    public sealed class HttpCallResponseBody
    {
        public string Text { get; set; }

        /// <summary>
        /// Parsed JSON token for the body (similar to typed <c>response.json()</c>): object → <see cref="JObject"/>,
        /// array → <see cref="JArray"/>, literals → <see cref="JValue"/>.
        /// </summary>
        public JToken Json { get; set; }
    }

    /// <summary>Unified return value from the HTTP Request activity.</summary>
    public sealed class HttpCallResponse
    {
        public int StatusCode { get; set; }

        public string ReasonPhrase { get; set; }

        /// <summary>
        /// Response headers; multiple values with the same name are folded into a single comma-separated value.
        /// </summary>
        public Dictionary<string, string> Headers { get; set; }

        public HttpCallResponseBody Body { get; set; }
    }
}
