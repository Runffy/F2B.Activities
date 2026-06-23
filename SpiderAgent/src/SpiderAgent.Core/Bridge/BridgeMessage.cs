using System.Text.Json;

namespace SpiderAgent.Core.Bridge;

public sealed class BridgeMessage
{
    public required string Type { get; init; }

    public string? SessionId { get; init; }

    public JsonElement? Payload { get; init; }

    public static BridgeMessage Create(string type, string? sessionId = null, object? payload = null)
        => new()
        {
            Type = type,
            SessionId = sessionId,
            Payload = payload is null ? null : JsonSerializer.SerializeToElement(payload)
        };
}
