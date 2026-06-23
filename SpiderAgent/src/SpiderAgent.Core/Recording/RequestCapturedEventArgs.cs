namespace SpiderAgent.Core.Recording;

public sealed class RequestCapturedEventArgs : EventArgs
{
    public required RecordedRequest Request { get; init; }

    public int TotalCount { get; init; }
}
