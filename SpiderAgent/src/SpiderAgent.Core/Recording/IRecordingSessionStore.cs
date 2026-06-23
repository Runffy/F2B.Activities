using SpiderAgent.Core.Recording;

namespace SpiderAgent.Core.Recording;

public interface IRecordingSessionStore
{
    string GetSessionsRootDirectory();

    string GetSessionDirectory(string sessionId);

    string GetSessionOutputDirectory(string sessionId);

    Task SaveSessionAsync(RecordingSession session, CancellationToken cancellationToken = default);

    Task<RecordingSession?> LoadSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkspaceSessionMetadata>> ListWorkspaceSessionsAsync(
        CancellationToken cancellationToken = default);

    Task SaveWorkspaceMetadataAsync(
        WorkspaceSessionMetadata metadata,
        CancellationToken cancellationToken = default);

    Task<WorkspaceSessionMetadata?> LoadWorkspaceMetadataAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

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

    Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default);
}
