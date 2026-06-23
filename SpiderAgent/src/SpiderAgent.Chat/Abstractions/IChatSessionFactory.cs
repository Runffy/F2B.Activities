using SpiderAgent.Chat.Models;

namespace SpiderAgent.Chat.Abstractions;

public interface IChatSessionFactory
{
    IChatSession Create(ChatSessionOptions? options = null);

    IChatSession CreateFromHistory(
        ChatSessionOptions options,
        IReadOnlyList<ChatMessage> history);
}
