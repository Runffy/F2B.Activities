using SpiderAgent.Chat.Models;

namespace SpiderAgent.Chat.Abstractions;

/// <summary>
/// 具体 LLM 提供商的实现接口。新增提供商时实现此接口并注册即可。
/// </summary>
public interface IChatProvider
{
    string Name { get; }

    Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default);

    IAsyncEnumerable<ChatStreamChunk> StreamAsync(
        ChatRequest request,
        CancellationToken cancellationToken = default);
}
