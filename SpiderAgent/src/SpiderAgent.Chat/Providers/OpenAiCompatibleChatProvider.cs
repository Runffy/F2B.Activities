using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SpiderAgent.Chat.Abstractions;
using SpiderAgent.Chat.Models;

namespace SpiderAgent.Chat.Providers;

/// <summary>
/// OpenAI Chat Completions 兼容协议的基础实现，DeepSeek 与多数内网模型均可复用。
/// </summary>
internal sealed class OpenAiCompatibleChatProvider : IChatProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly OpenAiCompatibleProviderSettings _settings;
    private readonly ILogger<OpenAiCompatibleChatProvider> _logger;

    public OpenAiCompatibleChatProvider(
        HttpClient httpClient,
        OpenAiCompatibleProviderSettings settings,
        ILogger<OpenAiCompatibleChatProvider> logger)
    {
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;
    }

    public string Name => _settings.ProviderName;

    public async Task<ChatResponse> CompleteAsync(
        ChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var payload = BuildPayload(request, stream: false);
        using var httpRequest = CreateHttpRequest(payload);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new ChatProviderException(
                Name,
                $"请求失败 ({(int)response.StatusCode}): {body}");
        }

        var completion = JsonSerializer.Deserialize<ChatCompletionResponse>(body, JsonOptions)
            ?? throw new ChatProviderException(Name, "响应解析失败。");

        var choice = completion.Choices?.FirstOrDefault()
            ?? throw new ChatProviderException(Name, "响应中未包含 choices。");

        return new ChatResponse
        {
            Content = choice.Message?.Content ?? string.Empty,
            FinishReason = choice.FinishReason,
            Model = completion.Model,
            Usage = MapUsage(completion.Usage)
        };
    }

    public async IAsyncEnumerable<ChatStreamChunk> StreamAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var payload = BuildPayload(request, stream: true);
        using var httpRequest = CreateHttpRequest(payload);

        using var response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new ChatProviderException(
                Name,
                $"流式请求失败 ({(int)response.StatusCode}): {errorBody}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var data = line["data:".Length..].Trim();
            if (data == "[DONE]")
            {
                yield return new ChatStreamChunk { Delta = string.Empty, IsFinished = true };
                yield break;
            }

            ChatCompletionResponse? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize<ChatCompletionResponse>(data, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "忽略无法解析的 SSE 数据: {Data}", data);
                continue;
            }

            var choice = chunk?.Choices?.FirstOrDefault();
            if (choice is null)
            {
                continue;
            }

            var delta = choice.Delta?.Content ?? string.Empty;
            var isFinished = !string.IsNullOrEmpty(choice.FinishReason);

            if (!string.IsNullOrEmpty(delta) || isFinished)
            {
                yield return new ChatStreamChunk
                {
                    Delta = delta,
                    IsFinished = isFinished,
                    FinishReason = choice.FinishReason
                };
            }
        }
    }

    private HttpRequestMessage CreateHttpRequest(object payload)
    {
        var endpoint = CombineUrl(_settings.BaseUrl, _settings.CompletionsPath);
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };

        if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        }

        return request;
    }

    private object BuildPayload(ChatRequest request, bool stream)
    {
        return new
        {
            model = _settings.Model,
            messages = request.Messages.Select(message => new
            {
                role = ToApiRole(message.Role),
                content = message.Content
            }),
            temperature = request.Options.Temperature,
            max_tokens = request.Options.MaxTokens,
            stream
        };
    }

    private static string ToApiRole(ChatRole role) => role switch
    {
        ChatRole.System => "system",
        ChatRole.User => "user",
        ChatRole.Assistant => "assistant",
        _ => "user"
    };

    private static string CombineUrl(string baseUrl, string path)
    {
        var normalizedBase = baseUrl.TrimEnd('/');
        var normalizedPath = path.StartsWith('/') ? path : $"/{path}";
        return $"{normalizedBase}{normalizedPath}";
    }

    private static ChatUsage? MapUsage(ChatCompletionUsage? usage)
    {
        if (usage is null)
        {
            return null;
        }

        return new ChatUsage
        {
            PromptTokens = usage.PromptTokens ?? 0,
            CompletionTokens = usage.CompletionTokens ?? 0,
            TotalTokens = usage.TotalTokens ?? 0
        };
    }

    private sealed class ChatCompletionResponse
    {
        public string? Model { get; set; }

        public List<ChatCompletionChoice>? Choices { get; set; }

        public ChatCompletionUsage? Usage { get; set; }
    }

    private sealed class ChatCompletionUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int? PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int? CompletionTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int? TotalTokens { get; set; }
    }

    private sealed class ChatCompletionChoice
    {
        public ChatCompletionMessage? Message { get; set; }

        public ChatCompletionMessage? Delta { get; set; }

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    private sealed class ChatCompletionMessage
    {
        public string? Content { get; set; }
    }
}
