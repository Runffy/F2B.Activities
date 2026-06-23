namespace SpiderAgent.Chat.Models;

public sealed class ChatUsage
{
    public int PromptTokens { get; init; }

    public int CompletionTokens { get; init; }

    public int TotalTokens { get; init; }
}
