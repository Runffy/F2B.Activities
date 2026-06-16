using SpiderAgent.Chat.Abstractions;
using SpiderAgent.Chat.Models;

namespace SpiderAgent.Chat.Services;

internal sealed class ChatSessionFactory : IChatSessionFactory
{
    private readonly IChatClient _chatClient;

    public ChatSessionFactory(IChatClient chatClient)
    {
        _chatClient = chatClient;
    }

    public IChatSession Create(ChatSessionOptions? options = null)
        => new ChatSession(_chatClient, options);

    public IChatSession CreateFromHistory(
        ChatSessionOptions options,
        IReadOnlyList<ChatMessage> history)
        => new ChatSession(_chatClient, options, history);
}
