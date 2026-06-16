namespace SpiderAgent.Chat.Models;

public sealed class ChatRequest
{
    public required IReadOnlyList<ChatMessage> Messages { get; init; }

    public ChatCompletionOptions Options { get; init; } = new();
}
