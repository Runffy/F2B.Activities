using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpiderAgent.Chat.Abstractions;
using SpiderAgent.Chat.Configuration;
using SpiderAgent.Chat.Constants;
using SpiderAgent.Chat.Models;

namespace SpiderAgent.Chat.Providers;

public sealed class DeepSeekChatProvider : IChatProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DeepSeekProviderOptions _options;
    private readonly ILoggerFactory _loggerFactory;
    private OpenAiCompatibleChatProvider? _inner;

    public DeepSeekChatProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<ChatModuleOptions> options,
        ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value.DeepSeek;
        _loggerFactory = loggerFactory;
    }

    public string Name => ChatProviderNames.DeepSeek;

    public Task<ChatResponse> CompleteAsync(
        ChatRequest request,
        CancellationToken cancellationToken = default)
        => GetInner().CompleteAsync(request, cancellationToken);

    public IAsyncEnumerable<ChatStreamChunk> StreamAsync(
        ChatRequest request,
        CancellationToken cancellationToken = default)
        => GetInner().StreamAsync(request, cancellationToken);

    private OpenAiCompatibleChatProvider GetInner()
    {
        if (_inner is not null)
        {
            return _inner;
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException(
                $"DeepSeek ApiKey 未配置。请设置环境变量 {DeepSeekOptionsConfigurator.ApiKeyEnvironmentVariable}，" +
                "或在 appsettings 的 Chat:DeepSeek:ApiKey 中填写。");
        }

        var httpClient = _httpClientFactory.CreateClient(nameof(DeepSeekChatProvider));
        httpClient.Timeout = _options.Timeout;

        _inner = new OpenAiCompatibleChatProvider(
            httpClient,
            new OpenAiCompatibleProviderSettings
            {
                ProviderName = ChatProviderNames.DeepSeek,
                BaseUrl = _options.BaseUrl,
                Model = _options.Model,
                CompletionsPath = _options.CompletionsPath,
                ApiKey = _options.ApiKey,
                Timeout = _options.Timeout
            },
            _loggerFactory.CreateLogger<OpenAiCompatibleChatProvider>());

        return _inner;
    }
}
