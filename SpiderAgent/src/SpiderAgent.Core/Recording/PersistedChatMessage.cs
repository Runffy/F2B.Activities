namespace SpiderAgent.Core.Recording;

/// <summary>
/// 持久化的聊天消息（role 为 System / User / Assistant 字符串）。
/// </summary>
public sealed class PersistedChatMessage
{
    public required string Role { get; init; }

    public required string Content { get; init; }
}
