namespace SpiderAgent.Chat.Configuration;

/// <summary>
/// 公司内部本地模型配置。默认不携带 API Key，适用于内网 OpenAI 兼容网关。
/// </summary>
public sealed class InternalModelProviderOptions
{
    public string BaseUrl { get; set; } = "http://127.0.0.1:8080";

    public string Model { get; set; } = "default";

    public string CompletionsPath { get; set; } = "/v1/chat/completions";

    /// <summary>
    /// 可选。内网网关若需要鉴权可填写；留空则不发送 Authorization 头。
    /// </summary>
    public string? ApiKey { get; set; }

    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(10);
}
