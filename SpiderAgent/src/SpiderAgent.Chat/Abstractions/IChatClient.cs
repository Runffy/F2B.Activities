using SpiderAgent.Chat.Models;

namespace SpiderAgent.Chat.Abstractions;

/// <summary>
/// 对外统一的 Chat 入口。业务层只依赖此接口，不感知具体 LLM 提供商。
/// </summary>
public interface IChatClient
{
    Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default);

    IAsyncEnumerable<ChatStreamChunk> StreamAsync(
        ChatRequest request,
        CancellationToken cancellationToken = default);
}
