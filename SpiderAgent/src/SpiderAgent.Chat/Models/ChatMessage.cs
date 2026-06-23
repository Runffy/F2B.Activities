namespace SpiderAgent.Chat.Models;

public sealed class ChatMessage
{
    public required ChatRole Role { get; init; }

    public required string Content { get; init; }
}
