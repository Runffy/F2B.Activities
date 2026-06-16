using System.Text;
using SpiderAgent.Chat.Abstractions;
using SpiderAgent.Chat.Models;

namespace SpiderAgent.Chat.Services;

internal sealed class ChatSession : IChatSession
{
    private readonly IChatClient _chatClient;
    private readonly ChatSessionOptions _sessionOptions;
    private readonly List<ChatMessage> _history = [];

    public ChatSession(IChatClient chatClient, ChatSessionOptions? options = null)
        : this(chatClient, options, initialHistory: null)
    {
    }

    internal ChatSession(
        IChatClient chatClient,
        ChatSessionOptions? options,
        IReadOnlyList<ChatMessage>? initialHistory)
    {
        _chatClient = chatClient;
        _sessionOptions = options ?? new ChatSessionOptions();

        if (initialHistory is { Count: > 0 })
        {
            _history.AddRange(initialHistory);
            return;
        }

        if (!string.IsNullOrWhiteSpace(_sessionOptions.SystemPrompt))
        {
            _history.Add(new ChatMessage
            {
                Role = ChatRole.System,
                Content = _sessionOptions.SystemPrompt
            });
        }
    }

    public IReadOnlyList<ChatMessage> History => _history;

    public async Task<ChatResponse> SendAsync(
        string userMessage,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        AppendUserMessage(userMessage);

        var response = await _chatClient.CompleteAsync(
            BuildRequest(options),
            cancellationToken);

        AppendAssistantMessage(response.Content);
        return response;
    }

    public async IAsyncEnumerable<ChatStreamChunk> StreamSendAsync(
        string userMessage,
        ChatCompletionOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        AppendUserMessage(userMessage);

        var assistantBuffer = new StringBuilder();
        var requestOptions = MergeOptions(options);
        requestOptions = new ChatCompletionOptions
        {
            Temperature = requestOptions.Temperature,
            MaxTokens = requestOptions.MaxTokens,
            Stream = true
        };

        await foreach (var chunk in _chatClient.StreamAsync(
                           BuildRequest(requestOptions),
                           cancellationToken))
        {
            if (!string.IsNullOrEmpty(chunk.Delta))
            {
                assistantBuffer.Append(chunk.Delta);
            }

            yield return chunk;

            if (chunk.IsFinished)
            {
                break;
            }
        }

        AppendAssistantMessage(assistantBuffer.ToString());
    }

    public void ClearHistory(bool keepSystemMessage = true)
    {
        if (keepSystemMessage)
        {
            var systemMessages = _history.Where(message => message.Role == ChatRole.System).ToList();
            _history.Clear();
            _history.AddRange(systemMessages);
            return;
        }

        _history.Clear();
    }

    private ChatRequest BuildRequest(ChatCompletionOptions? options)
        => new()
        {
            Messages = _history,
            Options = MergeOptions(options)
        };

    private ChatCompletionOptions MergeOptions(ChatCompletionOptions? options)
        => options ?? _sessionOptions.CompletionOptions;

    private void AppendUserMessage(string userMessage)
    {
        _history.Add(new ChatMessage
        {
            Role = ChatRole.User,
            Content = userMessage
        });
        TrimHistoryIfNeeded();
    }

    private void AppendAssistantMessage(string content)
    {
        _history.Add(new ChatMessage
        {
            Role = ChatRole.Assistant,
            Content = content
        });
        TrimHistoryIfNeeded();
    }

    private void TrimHistoryIfNeeded()
    {
        var maxMessages = _sessionOptions.MaxHistoryMessages;
        if (maxMessages is null or <= 0)
        {
            return;
        }

        var systemMessages = _history.Where(message => message.Role == ChatRole.System).ToList();
        var conversational = _history.Where(message => message.Role != ChatRole.System).ToList();

        if (conversational.Count <= maxMessages.Value)
        {
            return;
        }

        var trimmed = conversational
            .Skip(conversational.Count - maxMessages.Value)
            .ToList();

        _history.Clear();
        _history.AddRange(systemMessages);
        _history.AddRange(trimmed);
    }
}
