using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SpiderAgent.Core.Bridge;

namespace SpiderAgent.Recording.Bridge;

/// <summary>
/// 与 F2B Chromium Bridge 相同模式：HTTP /health + WebSocket 长连接，扩展侧持续重连。
/// </summary>
public sealed class ExtensionBridgeWebSocketServer : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILogger<ExtensionBridgeWebSocketServer> _logger;
    private readonly object _sync = new();
    private readonly CancellationTokenSource _cts = new();

    private HttpListener? _listener;
    private WebSocket? _extensionSocket;
    private Task? _acceptLoopTask;
    private Task? _heartbeatLoopTask;

    public ExtensionBridgeWebSocketServer(ILogger<ExtensionBridgeWebSocketServer> logger)
    {
        _logger = logger;
    }

    public bool IsExtensionConnected
    {
        get
        {
            lock (_sync)
            {
                return _extensionSocket?.State == WebSocketState.Open;
            }
        }
    }

    public event EventHandler<BridgeMessage>? MessageReceived;

    public event EventHandler<bool>? ConnectionChanged;

    public void Start()
    {
        if (_listener is not null)
        {
            return;
        }

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://{BridgeConstants.DefaultHost}:{BridgeConstants.DefaultPort}/");
        _listener.Start();

        _acceptLoopTask = Task.Run(() => AcceptLoopAsync(_cts.Token));
        _heartbeatLoopTask = Task.Run(() => HeartbeatLoopAsync(_cts.Token));

        _logger.LogInformation(
            "Extension Bridge 已启动: http://{Host}:{Port}{Health}, ws://{Host}:{Port}/",
            BridgeConstants.DefaultHost,
            BridgeConstants.DefaultPort,
            BridgeConstants.HealthPath,
            BridgeConstants.DefaultHost,
            BridgeConstants.DefaultPort);
    }

    public async Task SendAsync(BridgeMessage message, CancellationToken cancellationToken = default)
    {
        WebSocket? socket;
        lock (_sync)
        {
            socket = _extensionSocket;
        }

        if (socket is null || socket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("Chrome 扩展尚未连接。请确认 Chrome 已加载 SpiderAgent 扩展。");
        }

        var json = JsonSerializer.Serialize(message, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener is { IsListening: true })
        {
            HttpListenerContext? context = null;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == 995)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            if (context is null)
            {
                continue;
            }

            if (!context.Request.IsWebSocketRequest)
            {
                WriteHealthResponse(context);
                continue;
            }

            var wsContext = await context.AcceptWebSocketAsync(null).ConfigureAwait(false);
            _ = Task.Run(() => HandleExtensionSocketAsync(wsContext.WebSocket, cancellationToken), cancellationToken);
        }
    }

    private static void WriteHealthResponse(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath.TrimEnd('/') ?? string.Empty;
        if (string.IsNullOrEmpty(path))
        {
            path = "/";
        }

        if (!path.Equals(BridgeConstants.HealthPath, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            context.Response.Close();
            return;
        }

        var payload = "{\"ok\":true}"u8.ToArray();
        context.Response.StatusCode = (int)HttpStatusCode.OK;
        context.Response.ContentType = "application/json";
        context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
        context.Response.OutputStream.Write(payload);
        context.Response.Close();
    }

    private async Task HandleExtensionSocketAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        WebSocket? previous;
        lock (_sync)
        {
            previous = _extensionSocket;
            _extensionSocket = socket;
        }

        if (previous is not null && !ReferenceEquals(previous, socket))
        {
            await CloseSocketQuietlyAsync(previous);
        }

        SetConnected(true);
        _logger.LogInformation("Chrome 扩展 WebSocket 已连接。");

        try
        {
            await SendAsync(
                BridgeMessage.Create(BridgeMessageTypes.BridgeConnected),
                cancellationToken);

            var buffer = new byte[8192];
            var builder = new StringBuilder();

            while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                builder.Clear();
                WebSocketReceiveResult result;

                do
                {
                    result = await socket.ReceiveAsync(buffer, cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            string.Empty,
                            CancellationToken.None);
                        return;
                    }

                    builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
                while (!result.EndOfMessage);

                HandleIncomingMessage(builder.ToString());
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException ex)
        {
            _logger.LogDebug(ex, "Chrome 扩展 WebSocket 断开。");
        }
        finally
        {
            lock (_sync)
            {
                if (ReferenceEquals(_extensionSocket, socket))
                {
                    _extensionSocket = null;
                }
            }

            await CloseSocketQuietlyAsync(socket);
            SetConnected(false);
            _logger.LogInformation("Chrome 扩展 WebSocket 已断开。");
        }
    }

    private void HandleIncomingMessage(string json)
    {
        BridgeMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<BridgeMessage>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "无法解析扩展消息: {Json}", json);
            return;
        }

        if (message is null)
        {
            return;
        }

        if (message.Type is BridgeMessageTypes.Ping or BridgeMessageTypes.Pong or BridgeMessageTypes.Hello)
        {
            if (message.Type == BridgeMessageTypes.Ping)
            {
                _ = SendPongAsync();
            }

            return;
        }

        MessageReceived?.Invoke(this, message);
    }

    private async Task SendPongAsync()
    {
        try
        {
            await SendAsync(BridgeMessage.Create(BridgeMessageTypes.Pong));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "发送 pong 失败。");
        }
    }

    private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(BridgeConstants.HeartbeatIntervalSeconds),
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (!IsExtensionConnected)
            {
                continue;
            }

            try
            {
                await SendAsync(BridgeMessage.Create(BridgeMessageTypes.Ping), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "扩展心跳发送失败。");
            }
        }
    }

    private void SetConnected(bool connected)
    {
        ConnectionChanged?.Invoke(this, connected);
    }

    private static async Task CloseSocketQuietlyAsync(WebSocket socket)
    {
        try
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    string.Empty,
                    CancellationToken.None);
            }
        }
        catch
        {
        }
        finally
        {
            socket.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        WebSocket? socket;
        lock (_sync)
        {
            socket = _extensionSocket;
        }

        if (socket is { State: WebSocketState.Open })
        {
            try
            {
                await SendAsync(BridgeMessage.Create(BridgeMessageTypes.BridgeShutdown));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "发送 bridge_shutdown 失败。");
            }
        }

        await _cts.CancelAsync();
        _listener?.Close();
        _listener = null;

        lock (_sync)
        {
            socket = _extensionSocket;
            _extensionSocket = null;
        }

        if (socket is not null)
        {
            await CloseSocketQuietlyAsync(socket);
        }

        if (_acceptLoopTask is not null)
        {
            try
            {
                await _acceptLoopTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        if (_heartbeatLoopTask is not null)
        {
            try
            {
                await _heartbeatLoopTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _cts.Dispose();
    }
}
