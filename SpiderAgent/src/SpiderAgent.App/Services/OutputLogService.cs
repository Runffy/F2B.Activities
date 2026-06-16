using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace SpiderAgent.App.Services;

public sealed class OutputLogService : INotifyPropertyChanged, IDisposable
{
    private const int MaxLines = 2000;
    private const int ThrottleMs = 150;

    private readonly FileLogService _fileLog;
    private readonly StringBuilder _text = new();
    private readonly object _sync = new();

    private int? _streamingBlockStartOffset;
    private string? _pendingStreamingContent;
    private DispatcherTimer? _throttleTimer;

    public OutputLogService(FileLogService fileLog)
    {
        _fileLog = fileLog;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Text
    {
        get
        {
            lock (_sync)
            {
                return _text.ToString();
            }
        }
    }

    public void Append(string line)
    {
        _fileLog.WriteLine(line);

        lock (_sync)
        {
            if (_text.Length > 0)
            {
                _text.AppendLine();
            }

            _text.Append(line);
            TrimLinesIfNeeded();
        }

        NotifyTextChanged();
    }

    /// <summary>
    /// 标记流式输出区域起点。之后 <see cref="UpdateStreamingBlock"/> 只会替换该位置之后的内容。
    /// </summary>
    public void BeginStreamingBlock()
    {
        lock (_sync)
        {
            CancelPendingStreamingUpdate();

            if (_text.Length > 0)
            {
                _text.AppendLine();
            }

            _streamingBlockStartOffset = _text.Length;
        }
    }

    /// <summary>
    /// 在流式区域内写入最新内容（节流刷新 UI，不重复追加行）。
    /// </summary>
    public void UpdateStreamingBlock(string content, bool immediate = false)
    {
        lock (_sync)
        {
            _pendingStreamingContent = content;
        }

        if (immediate)
        {
            FlushPendingStreamingBlock();
            return;
        }

        EnsureThrottleTimer();
    }

    /// <summary>
    /// 结束流式区域，写入最终内容并清除区域标记。
    /// </summary>
    public void EndStreamingBlock(string finalContent)
    {
        lock (_sync)
        {
            CancelPendingStreamingUpdate();
            ApplyStreamingBlock(finalContent);
            _streamingBlockStartOffset = null;
        }

        NotifyTextChanged();
    }

    public void Clear()
    {
        lock (_sync)
        {
            CancelPendingStreamingUpdate();
            _text.Clear();
            _streamingBlockStartOffset = null;
        }

        NotifyTextChanged();
    }

    public void LoadText(string text)
    {
        lock (_sync)
        {
            CancelPendingStreamingUpdate();
            _text.Clear();
            if (!string.IsNullOrEmpty(text))
            {
                _text.Append(text);
            }

            _streamingBlockStartOffset = null;
        }

        NotifyTextChanged();
    }

    public void Dispose()
    {
        CancelPendingStreamingUpdate();
    }

    private void EnsureThrottleTimer()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            FlushPendingStreamingBlock();
            return;
        }

        if (_throttleTimer is not null)
        {
            return;
        }

        _throttleTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(ThrottleMs),
            DispatcherPriority.Background,
            (_, _) =>
            {
                _throttleTimer?.Stop();
                _throttleTimer = null;
                FlushPendingStreamingBlock();

                lock (_sync)
                {
                    if (_pendingStreamingContent is not null)
                    {
                        EnsureThrottleTimer();
                    }
                }
            },
            dispatcher);

        _throttleTimer.Start();
    }

    private void FlushPendingStreamingBlock()
    {
        string? content;
        lock (_sync)
        {
            content = _pendingStreamingContent;
            if (content is null)
            {
                return;
            }

            _pendingStreamingContent = null;
            ApplyStreamingBlock(content);
        }

        NotifyTextChanged();
    }

    private void ApplyStreamingBlock(string content)
    {
        if (_streamingBlockStartOffset is null)
        {
            Append(content);
            return;
        }

        _text.Length = _streamingBlockStartOffset.Value;
        _text.Append(content);
        TrimLinesIfNeeded();
    }

    private void CancelPendingStreamingUpdate()
    {
        _throttleTimer?.Stop();
        _throttleTimer = null;
        _pendingStreamingContent = null;
    }

    private void TrimLinesIfNeeded()
    {
        while (CountLines() > MaxLines)
        {
            var content = _text.ToString();
            var firstLineBreak = content.IndexOf('\n');
            if (firstLineBreak < 0)
            {
                _text.Clear();
                _streamingBlockStartOffset = null;
                break;
            }

            _text.Remove(0, firstLineBreak + 1);

            if (_streamingBlockStartOffset is not null)
            {
                _streamingBlockStartOffset = Math.Max(0, _streamingBlockStartOffset.Value - (firstLineBreak + 1));
                if (_streamingBlockStartOffset == 0 && _text.Length == 0)
                {
                    _streamingBlockStartOffset = null;
                }
            }
        }
    }

    private int CountLines()
    {
        if (_text.Length == 0)
        {
            return 0;
        }

        var count = 1;
        for (var i = 0; i < _text.Length; i++)
        {
            if (_text[i] == '\n')
            {
                count++;
            }
        }

        return count;
    }

    private void NotifyTextChanged()
    {
        var handler = PropertyChanged;
        if (handler is null)
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            handler(this, new PropertyChangedEventArgs(nameof(Text)));
            return;
        }

        dispatcher.BeginInvoke(
            () => handler(this, new PropertyChangedEventArgs(nameof(Text))),
            DispatcherPriority.Background);
    }
}
