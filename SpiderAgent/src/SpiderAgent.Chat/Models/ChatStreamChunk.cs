namespace SpiderAgent.Chat.Models;

public sealed class ChatStreamChunk
{
    public required string Delta { get; init; }

    public bool IsFinished { get; init; }

    public string? FinishReason { get; init; }
}
