using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpiderAgent.Chat.Abstractions;
using SpiderAgent.Chat.Configuration;
using SpiderAgent.Chat.Constants;

namespace SpiderAgent.Chat.Providers;

/// <summary>
/// 公司内部本地模型。默认无 API Key，仅配置 BaseUrl + Model 即可接入。
/// </summary>
public sealed class InternalModelChatProvider : IChatProvider
{
    private readonly OpenAiCompatibleChatProvider _inner;

    public InternalModelChatProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<ChatModuleOptions> options,
        ILoggerFactory loggerFactory)
    {
        var internalOptions = options.Value.InternalModel;

        if (string.IsNullOrWhiteSpace(internalOptions.BaseUrl))
        {
            throw new InvalidOperationException("InternalModel BaseUrl 未配置。");
        }

        var httpClient = httpClientFactory.CreateClient(nameof(InternalModelChatProvider));
        httpClient.Timeout = internalOptions.Timeout;

        var settings = new OpenAiCompatibleProviderSettings
        {
            ProviderName = ChatProviderNames.InternalModel,
            BaseUrl = internalOptions.BaseUrl,
            Model = internalOptions.Model,
            CompletionsPath = internalOptions.CompletionsPath,
            ApiKey = string.IsNullOrWhiteSpace(internalOptions.ApiKey)
                ? null
                : internalOptions.ApiKey,
            Timeout = internalOptions.Timeout
        };

        _inner = new OpenAiCompatibleChatProvider(
            httpClient,
            settings,
            loggerFactory.CreateLogger<OpenAiCompatibleChatProvider>());
    }

    public string Name => ChatProviderNames.InternalModel;

    public Task<Models.ChatResponse> CompleteAsync(
        Models.ChatRequest request,
        CancellationToken cancellationToken = default)
        => _inner.CompleteAsync(request, cancellationToken);

    public IAsyncEnumerable<Models.ChatStreamChunk> StreamAsync(
        Models.ChatRequest request,
        CancellationToken cancellationToken = default)
        => _inner.StreamAsync(request, cancellationToken);
}
