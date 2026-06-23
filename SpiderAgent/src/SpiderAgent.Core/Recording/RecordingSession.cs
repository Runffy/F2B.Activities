namespace SpiderAgent.Core.Recording;

public sealed class RecordingSession
{
    public required string SessionId { get; init; }

    public required RecordingBrowserMode BrowserMode { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset? StoppedAt { get; set; }

    public List<RecordedRequest> Requests { get; init; } = [];

    public List<RecordedScript> Scripts { get; init; } = [];
}
