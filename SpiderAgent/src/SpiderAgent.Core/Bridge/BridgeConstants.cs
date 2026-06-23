namespace SpiderAgent.Core.Bridge;

public static class BridgeConstants
{
    public const string DefaultHost = "127.0.0.1";

    public const int DefaultPort = 17654;

    public const string HealthPath = "/health";

    public const int HeartbeatIntervalSeconds = 25;

    public const int HeartbeatTimeoutSeconds = 90;

    /// <summary>保留给旧版 Native Host，WebSocket 模式下不再使用。</summary>
    public const string PipeName = "SpiderAgent.Recording";

    public const string NativeHostName = "com.spideragent.recorder";
}
