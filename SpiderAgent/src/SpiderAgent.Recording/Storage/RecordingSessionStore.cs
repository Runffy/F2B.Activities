using System.Text.Json;
using System.Text.Json.Serialization;
using SpiderAgent.Core.Recording;

namespace SpiderAgent.Recording.Storage;

public sealed class RecordingSessionStore : IRecordingSessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _sessionsRoot;
    private readonly string _outputRoot;

    public RecordingSessionStore()
    {
        _sessionsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SpiderAgent",
            "Sessions");
        Directory.CreateDirectory(_sessionsRoot);

        _outputRoot = Path.Combine(AppContext.BaseDirectory, "SpiderAgentOutput");
        Directory.CreateDirectory(_outputRoot);
    }

    public string GetSessionsRootDirectory() => _sessionsRoot;

    public string GetSessionDirectory(string sessionId)
        => Path.Combine(_sessionsRoot, sessionId);

    public string GetSessionOutputDirectory(string sessionId)
        => Path.Combine(_outputRoot, sessionId);

    public async Task SaveSessionAsync(RecordingSession session, CancellationToken cancellationToken = default)
    {
        var sessionDir = GetSessionDirectory(session.SessionId);
        Directory.CreateDirectory(sessionDir);

        var scriptsDir = Path.Combine(sessionDir, "scripts");
        Directory.CreateDirectory(scriptsDir);

        foreach (var script in session.Scripts.Where(script => !string.IsNullOrEmpty(script.Content)))
        {
            var scriptPath = Path.Combine(scriptsDir, $"{script.Id}.js");
            await File.WriteAllTextAsync(scriptPath, script.Content, cancellationToken);
        }

        var metadataPath = Path.Combine(sessionDir, "session.json");
        await using var stream = File.Create(metadataPath);
        await JsonSerializer.SerializeAsync(stream, session, JsonOptions, cancellationToken);
    }

    public async Task<RecordingSession?> LoadSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var metadataPath = Path.Combine(GetSessionDirectory(sessionId), "session.json");
        if (!File.Exists(metadataPath))
        {
            return null;
        }

        await using var stream = File.OpenRead(metadataPath);
        return await JsonSerializer.DeserializeAsync<RecordingSession>(stream, JsonOptions, cancellationToken);
    }

    public Task<IReadOnlyList<WorkspaceSessionMetadata>> ListWorkspaceSessionsAsync(
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("请使用 SessionStore（SQLite）列出工作会话。");

    public Task SaveWorkspaceMetadataAsync(
        WorkspaceSessionMetadata metadata,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("请使用 SessionStore（SQLite）保存工作会话元数据。");

    public Task<WorkspaceSessionMetadata?> LoadWorkspaceMetadataAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("请使用 SessionStore（SQLite）加载工作会话元数据。");

    public Task SaveChatHistoryAsync(
        string sessionId,
        IReadOnlyList<PersistedChatMessage> messages,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("请使用 SessionStore（SQLite）保存对话历史。");

    public Task<IReadOnlyList<PersistedChatMessage>?> LoadChatHistoryAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("请使用 SessionStore（SQLite）加载对话历史。");

    public Task SaveOutputLogTextAsync(
        string sessionId,
        string text,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("请使用 SessionStore（SQLite）保存 Output。");

    public Task<string> LoadOutputLogTextAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("请使用 SessionStore（SQLite）加载 Output。");

    public Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sessionDir = GetSessionDirectory(sessionId);
        if (Directory.Exists(sessionDir))
        {
            Directory.Delete(sessionDir, recursive: true);
        }

        var outputDir = GetSessionOutputDirectory(sessionId);
        if (Directory.Exists(outputDir))
        {
            Directory.Delete(outputDir, recursive: true);
        }

        return Task.CompletedTask;
    }
}
