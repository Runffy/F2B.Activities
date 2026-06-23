namespace SpiderAgent.Chat.Providers;

internal sealed class OpenAiCompatibleProviderSettings
{
    public required string ProviderName { get; init; }

    public required string BaseUrl { get; init; }

    public required string Model { get; init; }

    public string CompletionsPath { get; init; } = "/v1/chat/completions";

    public string? ApiKey { get; init; }

    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(5);
}
