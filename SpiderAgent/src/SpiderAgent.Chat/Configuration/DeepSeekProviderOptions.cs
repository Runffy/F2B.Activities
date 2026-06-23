namespace SpiderAgent.Chat.Configuration;

public sealed class DeepSeekProviderOptions
{
    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.deepseek.com";

    public string Model { get; set; } = "deepseek-chat";

    public string CompletionsPath { get; set; } = "/v1/chat/completions";

    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
}
