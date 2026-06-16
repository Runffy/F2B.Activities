namespace SpiderAgent.Core.Recording;

public sealed class RecordedScript
{
    public required string Id { get; init; }

    public required string Url { get; init; }

    public string? Content { get; init; }

    public bool LoadedBeforeAttach { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public int? TabId { get; init; }
}
