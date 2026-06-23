namespace SpiderAgent.Core.Recording;

public sealed class RecordedRequest
{
    public required string Id { get; init; }

    public required string Url { get; init; }

    public required string Method { get; init; }

    public int? StatusCode { get; init; }

    public string? ResourceType { get; init; }

    public string? RequestHeadersJson { get; init; }

    public string? RequestBody { get; init; }

    public string? ResponseHeadersJson { get; init; }

    public string? ResponseBody { get; init; }

    public bool ResponseBodyIsBase64 { get; init; }

    public string? MimeType { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public int? TabId { get; init; }
}
