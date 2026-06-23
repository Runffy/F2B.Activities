using System.IO.Pipes;
using System.Text;

namespace SpiderAgent.NativeHost;

internal sealed class PipeBridgeClient : IAsyncDisposable
{
    private NamedPipeClientStream? _pipe;
    private StreamWriter? _writer;
    private StreamReader? _reader;

    public async Task ConnectWithRetryAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _pipe = new NamedPipeClientStream(
                    ".",
                    SpiderAgent.Core.Bridge.BridgeConstants.PipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);

                await _pipe.ConnectAsync(3000, cancellationToken);
                _reader = new StreamReader(_pipe, Encoding.UTF8, leaveOpen: true);
                _writer = new StreamWriter(_pipe, new UTF8Encoding(false), leaveOpen: true)
                {
                    AutoFlush = true
                };
                return;
            }
            catch
            {
                await Task.Delay(1000, cancellationToken);
            }
        }

        throw new OperationCanceledException("连接 SpiderAgent 主程序失败。");
    }

    public async Task SendLineAsync(string line, CancellationToken cancellationToken)
    {
        if (_writer is null)
        {
            throw new InvalidOperationException("Pipe 未连接。");
        }

        await _writer.WriteLineAsync(line.AsMemory(), cancellationToken);
        await _writer.FlushAsync(cancellationToken);
    }

    public async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        if (_reader is null)
        {
            return null;
        }

        return await _reader.ReadLineAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        _writer?.Dispose();
        _reader?.Dispose();
        if (_pipe is not null)
        {
            await _pipe.DisposeAsync();
        }
    }
}
