using SpiderAgent.Core.Recording;

namespace SpiderAgent.App.Storage;

public interface ISessionDatabase
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkspaceSessionMetadata>> ListSessionsAsync(
        CancellationToken cancellationToken = default);

    Task<WorkspaceSessionMetadata?> GetSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    Task SaveSessionAsync(
        WorkspaceSessionMetadata metadata,
        CancellationToken cancellationToken = default);

    Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    Task SaveChatHistoryAsync(
        string sessionId,
        IReadOnlyList<PersistedChatMessage> messages,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PersistedChatMessage>?> LoadChatHistoryAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    Task SaveOutputLogTextAsync(
        string sessionId,
        string text,
        CancellationToken cancellationToken = default);

    Task<string> LoadOutputLogTextAsync(
        string sessionId,
        CancellationToken cancellationToken = default);
}
