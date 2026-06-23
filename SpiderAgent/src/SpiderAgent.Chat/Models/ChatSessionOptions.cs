namespace SpiderAgent.Chat.Models;

public sealed class ChatSessionOptions
{
    /// <summary>
    /// 会话级系统提示词，会作为首条 system 消息保留。
    /// </summary>
    public string? SystemPrompt { get; init; }

    /// <summary>
    /// 保留的最大消息条数（不含 system）。超出时从最早的用户/助手消息开始丢弃。
    /// null 表示不限制。
    /// </summary>
    public int? MaxHistoryMessages { get; init; }

    public ChatCompletionOptions CompletionOptions { get; init; } = new()
    {
        Temperature = 0.7,
        MaxTokens = 500,
        Stream = true
    };
}
