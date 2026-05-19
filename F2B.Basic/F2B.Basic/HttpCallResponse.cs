using System.Collections.Generic;

namespace F2B.Basic
{
    /// <summary>
    /// 用于承载 HTTP Response 正文：Text 等价 response.text（UTF-8 解码后的字符串）；
    /// Dict 等价 response.json() 在返回值为 JSON 顶层对象时的字典视图（否则为 null）。
    /// </summary>
    public sealed class HttpCallResponseBody
    {
        public string Text { get; set; }

        public Dictionary<string, object> Dict { get; set; }
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
