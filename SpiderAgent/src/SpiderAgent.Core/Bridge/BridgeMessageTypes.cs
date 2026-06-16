namespace SpiderAgent.Core.Bridge;

public static class BridgeMessageTypes
{
    public const string BridgeConnected = "bridge_connected";
    public const string BridgeShutdown = "bridge_shutdown";
    public const string StartRecording = "start_recording";
    public const string StopRecording = "stop_recording";
    public const string RecordingStarted = "recording_started";
    public const string RecordingStopped = "recording_stopped";
    public const string NetworkEvent = "network_event";
    public const string ScriptDiscovered = "script_discovered";
    public const string ScriptContent = "script_content";
    public const string Log = "log";
    public const string Error = "error";
    public const string Ping = "ping";
    public const string Pong = "pong";
    public const string Hello = "hello";
}
