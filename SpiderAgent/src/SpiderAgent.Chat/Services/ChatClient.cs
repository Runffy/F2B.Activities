using Microsoft.Extensions.Options;
using SpiderAgent.Chat.Abstractions;
using SpiderAgent.Chat.Configuration;
using SpiderAgent.Chat.Models;

namespace SpiderAgent.Chat.Services;

internal sealed class ChatClient : IChatClient
{
    private readonly IChatProvider _provider;

    public ChatClient(IChatProvider provider)
    {
        _provider = provider;
    }

    public Task<ChatResponse> CompleteAsync(
        ChatRequest request,
        CancellationToken cancellationToken = default)
        => _provider.CompleteAsync(request, cancellationToken);

    public IAsyncEnumerable<ChatStreamChunk> StreamAsync(
        ChatRequest request,
        CancellationToken cancellationToken = default)
        => _provider.StreamAsync(request, cancellationToken);
}
