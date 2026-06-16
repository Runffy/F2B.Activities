using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpiderAgent.Core.Bridge;
using SpiderAgent.Core.Recording;
using SpiderAgent.Recording.Bridge;
using SpiderAgent.Recording.Configuration;
using SpiderAgent.Recording.Storage;

namespace SpiderAgent.Recording;

public sealed class ChromeRecordingService : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ExtensionBridgeWebSocketServer _bridgeServer;
    private readonly IRecordingSessionStore _sessionStore;
    private readonly ILogger<ChromeRecordingService> _logger;
    private readonly RecordingOptions _options;

    private RecordingSession? _currentSession;
    private RecordingStatus _status = RecordingStatus.Idle;

    public ChromeRecordingService(
        ExtensionBridgeWebSocketServer bridgeServer,
        IRecordingSessionStore sessionStore,
        IOptions<RecordingOptions> options,
        ILogger<ChromeRecordingService> logger)
    {
        _bridgeServer = bridgeServer;
        _sessionStore = sessionStore;
        _logger = logger;
        _options = options.Value;
        _bridgeServer.MessageReceived += OnBridgeMessageReceived;
    }

    public RecordingStatus Status => _status;

    public bool IsBridgeConnected => _bridgeServer.IsExtensionConnected;

    public RecordingSession? CurrentSession => _currentSession;

    public event EventHandler<RecordingStatus>? StatusChanged;

    public event EventHandler<string>? LogReceived;

    public event EventHandler<bool>? BridgeConnectionChanged;

    public event EventHandler<int>? RequestCountChanged;

    public void StartBridgeServer()
    {
        _bridgeServer.Start();
        _bridgeServer.ConnectionChanged += (_, connected) =>
            BridgeConnectionChanged?.Invoke(this, connected);
    }

    public async Task StartRecordingAsync(
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        if (_status == RecordingStatus.Recording)
        {
            return;
        }

        var resolvedSessionId = string.IsNullOrWhiteSpace(sessionId)
            ? DateTime.Now.ToString("yyyyMMddHHmmss")
            : sessionId;

        _currentSession = new RecordingSession
        {
            SessionId = resolvedSessionId,
            BrowserMode = RecordingBrowserMode.Chromium,
            StartedAt = DateTimeOffset.Now
        };

        SetStatus(RecordingStatus.Recording);
        EmitLog($"开始录制，会话 ID: {resolvedSessionId}");

        await _bridgeServer.SendAsync(
            BridgeMessage.Create(BridgeMessageTypes.StartRecording, resolvedSessionId),
            cancellationToken);
    }

    public async Task StopRecordingAsync(CancellationToken cancellationToken = default)
    {
        if (_status != RecordingStatus.Recording || _currentSession is null)
        {
            return;
        }

        await _bridgeServer.SendAsync(
            BridgeMessage.Create(BridgeMessageTypes.StopRecording, _currentSession.SessionId),
            cancellationToken);

        _currentSession.StoppedAt = DateTimeOffset.Now;
        await _sessionStore.SaveSessionAsync(_currentSession, cancellationToken);

        EmitLog(
            $"停止录制。请求数: {_currentSession.Requests.Count}，脚本数: {_currentSession.Scripts.Count}，" +
            $"保存目录: {_sessionStore.GetSessionDirectory(_currentSession.SessionId)}");

        SetStatus(RecordingStatus.Stopped);
    }

    /// <summary>
    /// 重置为“新会话”状态。
    /// 调用此方法后，当前内存中的录制会话会被丢弃（之前如果调用过 StopRecordingAsync 则数据已落盘）。
    /// UI 计数会被清零，下一次 StartRecording 将创建全新的 SessionId。
    /// </summary>
    public void ResetToIdleForNewSession(bool emitLog = true)
    {
        if (_status == RecordingStatus.Recording)
        {
            // 简单处理：直接切状态，扩展侧的录制状态由上层 Stop 负责清理
            if (emitLog)
            {
                EmitLog("检测到仍在录制中，强制结束当前会话状态。");
            }
        }

        _currentSession = null;
        SetStatus(RecordingStatus.Idle);
        RequestCountChanged?.Invoke(this, 0);
        if (emitLog)
        {
            EmitLog("已重置为新会话，可开始全新的录制。");
        }
    }

    /// <summary>
    /// 从磁盘加载的录制会话载入内存，状态设为已停止（可继续分析/优化）。
    /// </summary>
    public void LoadSession(RecordingSession session, bool emitLog = true)
    {
        _currentSession = session;
        SetStatus(RecordingStatus.Stopped);
        RequestCountChanged?.Invoke(this, session.Requests.Count);
        if (emitLog)
        {
            EmitLog($"已加载录制会话 {session.SessionId}（请求 {session.Requests.Count} 条）。");
        }
    }

    public string GetExtensionDirectory()
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, _options.ChromeExtensionRelativePath)),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "chrome-extension")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "chrome-extension"))
        };

        return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
    }

    private void OnBridgeMessageReceived(object? sender, BridgeMessage message)
    {
        switch (message.Type)
        {
            case BridgeMessageTypes.BridgeConnected:
                EmitLog("Chrome 扩展已连接。");
                break;

            case BridgeMessageTypes.RecordingStarted:
                EmitLog("扩展已开始监听当前标签页网络请求。");
                break;

            case BridgeMessageTypes.RecordingStopped:
                EmitLog("扩展已停止监听。");
                break;

            case BridgeMessageTypes.NetworkEvent:
                HandleNetworkEvent(message);
                break;

            case BridgeMessageTypes.ScriptDiscovered:
            case BridgeMessageTypes.ScriptContent:
                HandleScriptEvent(message);
                break;

            case BridgeMessageTypes.Log:
                if (message.Payload is { } logPayload
                    && logPayload.TryGetProperty("message", out var logMessage))
                {
                    EmitLog(logMessage.GetString() ?? string.Empty);
                }
                break;

            case BridgeMessageTypes.Error:
                if (message.Payload is { } errorPayload
                    && errorPayload.TryGetProperty("message", out var errorMessage))
                {
                    EmitLog($"[扩展错误] {errorMessage.GetString()}");
                }
                break;
        }
    }

    private void HandleNetworkEvent(BridgeMessage message)
    {
        if (_currentSession is null || message.Payload is null)
        {
            return;
        }

        var entry = message.Payload.Value.Deserialize<RecordedRequestDto>(JsonOptions);
        if (entry is null || string.IsNullOrWhiteSpace(entry.Url))
        {
            return;
        }

        _currentSession.Requests.Add(new RecordedRequest
        {
            Id = entry.Id ?? Guid.NewGuid().ToString("N"),
            Url = entry.Url,
            Method = entry.Method ?? "GET",
            StatusCode = entry.StatusCode,
            ResourceType = entry.ResourceType,
            RequestHeadersJson = entry.RequestHeadersJson,
            RequestBody = entry.RequestBody,
            ResponseHeadersJson = entry.ResponseHeadersJson,
            ResponseBody = entry.ResponseBody,
            ResponseBodyIsBase64 = entry.ResponseBodyIsBase64,
            MimeType = entry.MimeType,
            Timestamp = DateTimeOffset.TryParse(entry.Timestamp, out var ts) ? ts : DateTimeOffset.Now,
            TabId = entry.TabId
        });

        RequestCountChanged?.Invoke(this, _currentSession.Requests.Count);
    }

    private void HandleScriptEvent(BridgeMessage message)
    {
        if (_currentSession is null || message.Payload is null)
        {
            return;
        }

        var script = message.Payload.Value.Deserialize<RecordedScriptDto>(JsonOptions);
        if (script is null || string.IsNullOrWhiteSpace(script.Url))
        {
            return;
        }

        var existing = _currentSession.Scripts.FirstOrDefault(item => item.Url == script.Url);
        if (existing is not null)
        {
            if (!string.IsNullOrEmpty(script.Content))
            {
                _currentSession.Scripts.Remove(existing);
                _currentSession.Scripts.Add(new RecordedScript
                {
                    Id = existing.Id,
                    Url = script.Url,
                    Content = script.Content,
                    LoadedBeforeAttach = script.LoadedBeforeAttach ?? existing.LoadedBeforeAttach,
                    Timestamp = existing.Timestamp,
                    TabId = script.TabId ?? existing.TabId
                });
            }

            return;
        }

        _currentSession.Scripts.Add(new RecordedScript
        {
            Id = script.Id ?? Guid.NewGuid().ToString("N"),
            Url = script.Url,
            Content = script.Content,
            LoadedBeforeAttach = script.LoadedBeforeAttach ?? false,
            Timestamp = DateTimeOffset.TryParse(script.Timestamp, out var ts) ? ts : DateTimeOffset.Now,
            TabId = script.TabId
        });
    }

    private void SetStatus(RecordingStatus status)
    {
        _status = status;
        StatusChanged?.Invoke(this, status);
    }

    private void EmitLog(string message)
    {
        _logger.LogInformation("{Message}", message);
        LogReceived?.Invoke(this, message);
    }

    public async ValueTask DisposeAsync()
    {
        _bridgeServer.MessageReceived -= OnBridgeMessageReceived;
        await _bridgeServer.DisposeAsync();
    }

    private sealed class RecordedRequestDto
    {
        public string? Id { get; set; }
        public string? Url { get; set; }
        public string? Method { get; set; }
        public int? StatusCode { get; set; }
        public string? ResourceType { get; set; }
        public string? RequestHeadersJson { get; set; }
        public string? RequestBody { get; set; }
        public string? ResponseHeadersJson { get; set; }
        public string? ResponseBody { get; set; }
        public bool ResponseBodyIsBase64 { get; set; }
        public string? MimeType { get; set; }
        public string? Timestamp { get; set; }
        public int? TabId { get; set; }
    }

    private sealed class RecordedScriptDto
    {
        public string? Id { get; set; }
        public string? Url { get; set; }
        public string? Content { get; set; }
        public bool? LoadedBeforeAttach { get; set; }
        public string? Timestamp { get; set; }
        public int? TabId { get; set; }
    }
}
