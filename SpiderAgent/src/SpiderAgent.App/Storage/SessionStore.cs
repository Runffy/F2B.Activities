using SpiderAgent.App.Services;
using SpiderAgent.Core.Recording;
using SpiderAgent.Recording.Storage;

namespace SpiderAgent.App.Storage;

/// <summary>
/// 录制 JSON 走 Legacy 目录；元数据、对话、Output 文本走 SQLite。
/// </summary>
public sealed class SessionStore : IRecordingSessionStore
{
    private readonly RecordingSessionStore _recordingStore;
    private readonly ISessionDatabase _database;
    private readonly AppPaths _paths;

    public SessionStore(
        RecordingSessionStore recordingStore,
        ISessionDatabase database,
        AppPaths paths)
    {
        _recordingStore = recordingStore;
        _database = database;
        _paths = paths;
    }

    public string GetSessionsRootDirectory() => _recordingStore.GetSessionsRootDirectory();

    public string GetSessionDirectory(string sessionId) => _recordingStore.GetSessionDirectory(sessionId);

    public string GetSessionOutputDirectory(string sessionId) => _paths.GetSessionOutputDirectory(sessionId);

    public Task SaveSessionAsync(RecordingSession session, CancellationToken cancellationToken = default)
        => _recordingStore.SaveSessionAsync(session, cancellationToken);

    public Task<RecordingSession?> LoadSessionAsync(string sessionId, CancellationToken cancellationToken = default)
        => _recordingStore.LoadSessionAsync(sessionId, cancellationToken);

    public Task<IReadOnlyList<WorkspaceSessionMetadata>> ListWorkspaceSessionsAsync(
        CancellationToken cancellationToken = default)
        => _database.ListSessionsAsync(cancellationToken);

    public Task SaveWorkspaceMetadataAsync(
        WorkspaceSessionMetadata metadata,
        CancellationToken cancellationToken = default)
        => _database.SaveSessionAsync(metadata, cancellationToken);

    public Task<WorkspaceSessionMetadata?> LoadWorkspaceMetadataAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
        => _database.GetSessionAsync(sessionId, cancellationToken);

    public Task SaveChatHistoryAsync(
        string sessionId,
        IReadOnlyList<PersistedChatMessage> messages,
        CancellationToken cancellationToken = default)
        => _database.SaveChatHistoryAsync(sessionId, messages, cancellationToken);

    public Task<IReadOnlyList<PersistedChatMessage>?> LoadChatHistoryAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
        => _database.LoadChatHistoryAsync(sessionId, cancellationToken);

    public async Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        await _recordingStore.DeleteSessionAsync(sessionId, cancellationToken);
        await _database.DeleteSessionAsync(sessionId, cancellationToken);
    }

    public Task SaveOutputLogTextAsync(string sessionId, string text, CancellationToken cancellationToken = default)
        => _database.SaveOutputLogTextAsync(sessionId, text, cancellationToken);

    public Task<string> LoadOutputLogTextAsync(string sessionId, CancellationToken cancellationToken = default)
        => _database.LoadOutputLogTextAsync(sessionId, cancellationToken);
}
