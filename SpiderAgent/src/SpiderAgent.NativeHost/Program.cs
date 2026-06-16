using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using SpiderAgent.Core.Bridge;

namespace SpiderAgent.NativeHost;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static async Task Main()
    {
        using var stdin = Console.OpenStandardInput();
        using var stdout = Console.OpenStandardOutput();

        await using var pipeClient = new PipeBridgeClient();
        await pipeClient.ConnectWithRetryAsync(CancellationToken.None);

        await WriteChromeMessageAsync(
            stdout,
            BridgeMessage.Create(BridgeMessageTypes.Log, payload: new { message = "Native Host 已启动" }),
            CancellationToken.None);

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, args) =>
        {
            args.Cancel = true;
            cts.Cancel();
        };

        var chromeToPipe = PumpChromeToPipeAsync(stdin, stdout, pipeClient, cts.Token);
        var pipeToChrome = PumpPipeToChromeAsync(stdout, pipeClient, cts.Token);
        await Task.WhenAny(chromeToPipe, pipeToChrome);
        cts.Cancel();

        try
        {
            await Task.WhenAll(chromeToPipe, pipeToChrome);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static async Task PumpChromeToPipeAsync(
        Stream stdin,
        Stream stdout,
        PipeBridgeClient pipeClient,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var message = await ReadChromeMessageAsync(stdin, cancellationToken);
            if (message is null)
            {
                break;
            }

            if (message.Type == BridgeMessageTypes.Ping)
            {
                await WriteChromeMessageAsync(
                    stdout,
                    BridgeMessage.Create(BridgeMessageTypes.Pong),
                    cancellationToken);
                continue;
            }

            await pipeClient.SendLineAsync(JsonSerializer.Serialize(message, JsonOptions), cancellationToken);
        }
    }

    private static async Task PumpPipeToChromeAsync(
        Stream stdout,
        PipeBridgeClient pipeClient,
        CancellationToken cancellationToken)
    {
        await pipeClient.SendLineAsync(
            JsonSerializer.Serialize(BridgeMessage.Create(BridgeMessageTypes.BridgeConnected), JsonOptions),
            cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await pipeClient.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            var message = JsonSerializer.Deserialize<BridgeMessage>(line, JsonOptions);
            if (message is not null)
            {
                await WriteChromeMessageAsync(stdout, message, cancellationToken);
            }
        }
    }

    private static async Task<BridgeMessage?> ReadChromeMessageAsync(Stream stdin, CancellationToken cancellationToken)
    {
        var lengthBuffer = new byte[4];
        var read = await ReadExactAsync(stdin, lengthBuffer, cancellationToken);
        if (read == 0)
        {
            return null;
        }

        if (read < 4)
        {
            throw new InvalidOperationException("Native messaging 长度头不完整。");
        }

        var length = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);
        if (length <= 0 || length > 10 * 1024 * 1024)
        {
            throw new InvalidOperationException($"Native messaging 消息长度非法: {length}");
        }

        var payload = new byte[length];
        await ReadExactAsync(stdin, payload, cancellationToken);
        return JsonSerializer.Deserialize<BridgeMessage>(payload, JsonOptions);
    }

    private static async Task WriteChromeMessageAsync(
        Stream stdout,
        BridgeMessage message,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
        var lengthBuffer = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(lengthBuffer, payload.Length);
        await stdout.WriteAsync(lengthBuffer, cancellationToken);
        await stdout.WriteAsync(payload, cancellationToken);
        await stdout.FlushAsync(cancellationToken);
    }

    private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken);
            if (read == 0)
            {
                return offset;
            }

            offset += read;
        }

        return offset;
    }
}
