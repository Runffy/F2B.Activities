using SpiderAgent.Chat.Models;

namespace SpiderAgent.Chat.Abstractions;

/// <summary>
/// 带历史记录的对话会话，供 Agent 多轮推理使用。
/// </summary>
public interface IChatSession
{
    IReadOnlyList<ChatMessage> History { get; }

    Task<ChatResponse> SendAsync(
        string userMessage,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<ChatStreamChunk> StreamSendAsync(
        string userMessage,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default);

    void ClearHistory(bool keepSystemMessage = true);
}
