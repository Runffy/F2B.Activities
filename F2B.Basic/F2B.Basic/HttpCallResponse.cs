using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace F2B.Basic
{
    /// <summary>
    /// 用于承载 HTTP Response 正文：Text 等价 response.text（UTF-8 解码后的字符串）；
    /// Json 为成功解析正文得到的 <see cref="JToken"/>（顶层可为 <see cref="JObject"/>、<see cref="JArray"/> 等），无法解析或非 JSON 时为 null。
    /// </summary>
    public sealed class HttpCallResponseBody
    {
        public string Text { get; set; }

        /// <summary>
        /// 应答正文的 JSON 视图；等价按类型解析后的 <c>response.json()</c>。顶层对象为 <see cref="JObject"/>，数组为 <see cref="JArray"/>，字面量等为 <see cref="JValue"/>。
        /// </summary>
        public JToken Json { get; set; }
    }

    /// <summary>HTTP Request 活动的统一返回值。</summary>
    public sealed class HttpCallResponse
    {
        public int StatusCode { get; set; }

        public string ReasonPhrase { get; set; }

        /// <summary>同名响应头出现多次时合并为单个值（逗号+空格分隔）。</summary>
        public Dictionary<string, string> Headers { get; set; }

        public HttpCallResponseBody Body { get; set; }
    }
}
