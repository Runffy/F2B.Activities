namespace SpiderAgent.Chat.Models;

public sealed class ChatCompletionOptions
{
    public double Temperature { get; init; } = 0.2;

    public int? MaxTokens { get; init; }

    public bool Stream { get; init; } = true;
}
