namespace SpiderAgent.Core.Recording;

/// <summary>
/// 工作区会话元数据（存于 SQLite；录制数据仍在 session.json）。
/// </summary>
public sealed class WorkspaceSessionMetadata
{
    public required string SessionId { get; init; }

    /// <summary>由 Agent 根据首句 Prompt 自动总结。</summary>
    public string Title { get; set; } = string.Empty;

    public string? FirstPrompt { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; set; }

    public string? LastOutputScriptPath { get; set; }

    public bool HasAnalysisHistory { get; set; }

    public bool HasRecording { get; set; }
}
