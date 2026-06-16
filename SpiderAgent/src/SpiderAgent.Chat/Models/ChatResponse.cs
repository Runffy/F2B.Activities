namespace SpiderAgent.Chat.Models;

public sealed class ChatResponse
{
    public required string Content { get; init; }

    public string? FinishReason { get; init; }

    public string? Model { get; init; }

    public ChatUsage? Usage { get; init; }
}
