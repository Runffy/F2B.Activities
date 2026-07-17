using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Inspector.Models;
using FlaUI.Inspector.Overlays;

namespace FlaUI.Inspector.Services
{
    public sealed class IndicateService : IDisposable
    {
        private readonly ElementDetector _detector = new ElementDetector();
        private readonly GlobalInputHook _inputHook = new GlobalInputHook();
        private readonly Dispatcher _dispatcher;
        private IndicateOverlay _overlay;
        private CountdownOverlay _countdownOverlay;
        private F2HotKeyWindow _f2HotKeyWindow;
        private CancellationTokenSource _pauseCts;
        private DateTime _lastMoveTime = DateTime.MinValue;
        private Point _lastPoint = Point.Empty;
        private bool _isActive;
        private bool _inputCaptureArmed;
        private bool _isPaused;
        private bool _disposed;
        private int _clickInFlight;
        private const int DwellMilliseconds = 100;
        private const int PauseSeconds = 3;

        public event Action<IndicateCaptureResult> ElementSelected;
        public event Action Cancelled;
        public event Action<AutomationElement> ElementHovered;

        public IndicateService(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public void Start()
        {
            if (_isActive)
                return;

            _isActive = true;
            _inputCaptureArmed = true;
            _isPaused = false;
            _lastMoveTime = DateTime.MinValue;
            Interlocked.Exchange(ref _clickInFlight, 0);

            _dispatcher.Invoke(() =>
            {
                _overlay = new IndicateOverlay();
                _f2HotKeyWindow = new F2HotKeyWindow();
                _f2HotKeyWindow.F2Pressed += OnF2Pressed;
                _f2HotKeyWindow.EscapePressed += OnEscapePressed;
                if (!_f2HotKeyWindow.TryRegister())
                    System.Diagnostics.Debug.WriteLine("FlaUI.Inspector: failed to register F2/Esc hotkeys.");
            });

            _inputHook.ConsumeMouseClick = true;
            _inputHook.MouseMoved += OnMouseMoved;
            _inputHook.MouseButtonDown += OnMouseButtonDown;
            _inputHook.Start();
        }

        public void Stop()
        {
            ReleaseInputCapture(waitForConfirmingClickUp: false);
        }

        /// <summary>
        /// 立即停止鼠标/键盘捕获。确认点击后、selector 分析前必须调用，避免分析期间继续拦截输入。
        /// </summary>
        private void ReleaseInputCapture(bool waitForConfirmingClickUp)
        {
            _isActive = false;
            _isPaused = false;
            _pauseCts?.Cancel();

            if (!_inputCaptureArmed)
                return;

            _inputCaptureArmed = false;

            _inputHook.MouseMoved -= OnMouseMoved;
            _inputHook.MouseButtonDown -= OnMouseButtonDown;
            // 新点击立即放行；仅保留对确认点击配对 UP 的吞掉，避免目标控件残留按下态。
            _inputHook.ConsumeMouseClick = false;

            if (waitForConfirmingClickUp)
                _inputHook.WaitForPendingButtonUp(timeoutMs: 500);

            _inputHook.Stop();

            if (_dispatcher.CheckAccess())
                CloseOverlays();
            else
                _dispatcher.Invoke(CloseOverlays);
        }

        private void CloseOverlays()
        {
            _overlay?.Close();
            _overlay = null;
            _countdownOverlay?.Close();
            _countdownOverlay = null;

            if (_f2HotKeyWindow != null)
            {
                _f2HotKeyWindow.F2Pressed -= OnF2Pressed;
                _f2HotKeyWindow.EscapePressed -= OnEscapePressed;
                _f2HotKeyWindow.Dispose();
                _f2HotKeyWindow = null;
            }
        }

        private void OnMouseMoved(int x, int y)
        {
            if (!_isActive || _isPaused)
                return;

            _lastPoint = new Point(x, y);
            _lastMoveTime = DateTime.UtcNow;

            Task.Run(async () =>
            {
                var capturedPoint = _lastPoint;
                var capturedTime = _lastMoveTime;
                await Task.Delay(DwellMilliseconds).ConfigureAwait(false);

                if (!_isActive || _isPaused)
                    return;
                if (_lastPoint != capturedPoint || _lastMoveTime != capturedTime)
                    return;

                var element = _detector.DetectAtPoint(capturedPoint.X, capturedPoint.Y);
                if (element == null)
                    return;

                var rect = ElementDetector.GetBoundingRectangle(element);
                if (rect.IsEmpty)
                    return;

                _dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!_isActive || _isPaused)
                        return;

                    _overlay?.UpdateHighlight(rect);
                    ElementHovered?.Invoke(element);
                }), DispatcherPriority.Normal);
            });
        }

        private void OnMouseButtonDown(int x, int y)
        {
            if (!_isActive || _isPaused)
                return;

            // 钩子回调必须快速返回；同步 Dispatcher.Invoke + Capture 会触发 LowLevelHooksTimeout，导致点击穿透。
            if (Interlocked.CompareExchange(ref _clickInFlight, 1, 0) != 0)
                return;

            Task.Run(() =>
            {
                try
                {
                    HandleMouseClick(x, y);
                }
                finally
                {
                    Interlocked.Exchange(ref _clickInFlight, 0);
                }
            });
        }

        private void HandleMouseClick(int x, int y)
        {
            if (!_isActive || _isPaused)
                return;

            var element = _detector.DetectAtPoint(x, y);
            if (element == null)
                return;

            // 确认选中后立刻停止鼠标/键盘捕获，再分析 selector（分析期间不再拦截输入）。
            ReleaseInputCapture(waitForConfirmingClickUp: true);

            IndicateCaptureResult capture;
            try
            {
                capture = IndicateCaptureService.Capture(element);
            }
            catch
            {
                _dispatcher.BeginInvoke(new Action(() => Cancelled?.Invoke()));
                return;
            }

            if (capture == null)
            {
                _dispatcher.BeginInvoke(new Action(() => Cancelled?.Invoke()));
                return;
            }

            _dispatcher.BeginInvoke(new Action(() => ElementSelected?.Invoke(capture)));
        }

        private void OnEscapePressed()
        {
            if (!_isActive)
                return;

            // 推迟 Stop，避免在热键窗口 WndProc 内 Dispose 自身。
            _dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_isActive)
                    return;

                Stop();
                Cancelled?.Invoke();
            }));
        }

        private void OnF2Pressed()
        {
            if (!_isActive || _isPaused)
                return;

            BeginPauseCountdown();
        }

        private void BeginPauseCountdown()
        {
            _isPaused = true;
            _inputHook.ConsumeMouseClick = false;
            _pauseCts?.Cancel();
            _pauseCts = new CancellationTokenSource();
            var token = _pauseCts.Token;

            _dispatcher.Invoke(() =>
            {
                _f2HotKeyWindow?.UnregisterF2();
                _overlay?.HideHighlight();
                _countdownOverlay?.Close();
                _countdownOverlay = new CountdownOverlay();
                _countdownOverlay.ShowCountdown(PauseSeconds);
            });

            Task.Run(async () =>
            {
                try
                {
                    for (var i = PauseSeconds; i >= 1; i--)
                    {
                        var value = i;
                        _dispatcher.Invoke(() => _countdownOverlay?.UpdateCount(value));
                        await Task.Delay(1000, token).ConfigureAwait(false);
                    }

                    _dispatcher.Invoke(ResumeIndicate);
                }
                catch (TaskCanceledException)
                {
                }
            }, token);
        }

        private void ResumeIndicate()
        {
            _countdownOverlay?.Close();
            _countdownOverlay = null;

            if (!_isActive || !_inputCaptureArmed)
                return;

            _isPaused = false;
            _inputHook.ConsumeMouseClick = true;
            _lastMoveTime = DateTime.MinValue;

            _dispatcher.Invoke(() => _f2HotKeyWindow?.TryRegister());
            // 不主动 Show 高亮框，等下一次鼠标悬停再显示，避免抢焦点导致菜单收起
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Stop();
            _inputHook.Dispose();
            _disposed = true;
        }
    }
}
