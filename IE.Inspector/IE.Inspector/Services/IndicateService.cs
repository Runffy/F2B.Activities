using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using IE.Inspector.Models;
using IE.Inspector.Overlays;

namespace IE.Inspector.Services
{
    public sealed class IndicateService : IDisposable
    {
        private readonly GlobalInputHook _inputHook = new GlobalInputHook();
        private readonly Dispatcher _dispatcher;
        private IndicateOverlay _overlay;
        private CountdownOverlay _countdownOverlay;
        private IndicateProcessingOverlay _processingOverlay;
        private IndicateHotKeyWindow _hotKeyWindow;
        private CancellationTokenSource _pauseCts;
        private CancellationTokenSource _hoverCts;
        private int _hoverGeneration;
        private int _hoverInFlight;
        private DateTime _lastMoveTime = DateTime.MinValue;
        private System.Drawing.Point _lastPoint = System.Drawing.Point.Empty;
        private bool _isActive;
        private bool _isPaused;
        private bool _disposed;
        private const int DwellMilliseconds = 120;
        private const int PauseSeconds = 3;

        public event Action<IndicateCaptureResult> ElementSelected;
        public event Action IndicationCancelled;

        public IndicateService(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public void Start()
        {
            if (_isActive)
                return;

            _isActive = true;
            _isPaused = false;
            _hoverGeneration = 0;
            _lastMoveTime = DateTime.MinValue;
            _hoverCts?.Cancel();
            _hoverCts = new CancellationTokenSource();
            IeElementDetector.BeginSession();

            _dispatcher.Invoke(() =>
            {
                DismissProcessingOverlayCore();
                _overlay = new IndicateOverlay();
                _hotKeyWindow = new IndicateHotKeyWindow();
                _hotKeyWindow.F2Pressed += OnF2Pressed;
                _hotKeyWindow.EscapePressed += OnEscapePressed;
                if (!_hotKeyWindow.TryRegister())
                    System.Diagnostics.Debug.WriteLine("IE.Inspector: failed to register indicate hotkeys.");
            });

            _inputHook.ConsumeMouseClick = true;
            _inputHook.MouseMoved += OnMouseMoved;
            _inputHook.MouseButtonDown += OnMouseButtonDown;
            _inputHook.EscapePressed += OnEscapePressed;
            _inputHook.Start(captureEscape: true);
        }

        public void Stop(bool preserveProcessingOverlay = false)
        {
            if (!_isActive)
                return;

            _isActive = false;
            _isPaused = false;
            _pauseCts?.Cancel();
            _hoverCts?.Cancel();
            Interlocked.Exchange(ref _hoverInFlight, 0);

            _inputHook.ConsumeMouseClick = false;
            _inputHook.MouseMoved -= OnMouseMoved;
            _inputHook.MouseButtonDown -= OnMouseButtonDown;
            _inputHook.EscapePressed -= OnEscapePressed;
            _inputHook.Stop();
            IeElementDetector.EndSession();

            _dispatcher.Invoke(() => CloseOverlays(preserveProcessingOverlay));
        }

        public void DismissProcessingOverlay()
        {
            _dispatcher.Invoke(DismissProcessingOverlayCore);
        }

        private void CancelIndicate()
        {
            if (!_isActive)
                return;

            Stop();
            IndicationCancelled?.Invoke();
        }

        private void CloseOverlays(bool preserveProcessingOverlay = false)
        {
            _overlay?.Close();
            _overlay = null;
            _countdownOverlay?.Close();
            _countdownOverlay = null;

            if (!preserveProcessingOverlay)
                DismissProcessingOverlayCore();

            if (_hotKeyWindow != null)
            {
                _hotKeyWindow.F2Pressed -= OnF2Pressed;
                _hotKeyWindow.EscapePressed -= OnEscapePressed;
                _hotKeyWindow.Dispose();
                _hotKeyWindow = null;
            }
        }

        private void DismissProcessingOverlayCore()
        {
            _processingOverlay?.HideProcessing();
            _processingOverlay?.Close();
            _processingOverlay = null;
        }

        private void ShowProcessingOverlay()
        {
            if (_processingOverlay == null)
                _processingOverlay = new IndicateProcessingOverlay();

            _overlay?.HideHighlight();
            _processingOverlay.ShowProcessing();
        }

        private void OnEscapePressed()
        {
            if (!_isActive)
                return;

            _dispatcher.BeginInvoke(new Action(CancelIndicate), DispatcherPriority.Send);
        }

        private void OnMouseMoved(int x, int y)
        {
            if (!_isActive || _isPaused)
                return;

            _lastPoint = new System.Drawing.Point(x, y);
            _lastMoveTime = DateTime.UtcNow;
            Interlocked.Increment(ref _hoverGeneration);

            _dispatcher.BeginInvoke(new Action(() => _overlay?.HideHighlight()), DispatcherPriority.Send);

            if (Interlocked.CompareExchange(ref _hoverInFlight, 1, 0) != 0)
                return;

            var generation = _hoverGeneration;
            var hoverToken = _hoverCts?.Token ?? CancellationToken.None;

            Task.Run(async () =>
            {
                try
                {
                    var capturedPoint = _lastPoint;
                    var capturedTime = _lastMoveTime;
                    await Task.Delay(DwellMilliseconds, hoverToken).ConfigureAwait(false);

                    if (!_isActive || _isPaused || hoverToken.IsCancellationRequested)
                        return;
                    if (generation != _hoverGeneration)
                        return;
                    if (_lastPoint != capturedPoint || _lastMoveTime != capturedTime)
                        return;

                    // COM/MSHTML 必须在同一条 STA 线程（UI 线程）上调用；在线程池上调用会导致 RCW 随机失效。
                    _dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (!_isActive || _isPaused)
                            return;
                        if (generation != _hoverGeneration)
                            return;
                        if (_lastPoint != capturedPoint || _lastMoveTime != capturedTime)
                            return;

                        var detect = IeElementDetector.DetectAtPointForHover(capturedPoint.X, capturedPoint.Y);
                        if (detect?.Element == null)
                        {
                            _overlay?.HideHighlight();
                            return;
                        }

                        if (!detect.Bounds.IsEmpty)
                            _overlay?.UpdateHighlight(detect.Bounds);
                        else
                            _overlay?.HideHighlight();
                    }), DispatcherPriority.Background);
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    Interlocked.Exchange(ref _hoverInFlight, 0);
                    if (_isActive && !_isPaused && generation != _hoverGeneration)
                        _dispatcher.BeginInvoke(new Action(() => OnMouseMoved(_lastPoint.X, _lastPoint.Y)), DispatcherPriority.Background);
                }
            }, hoverToken);
        }

        private void OnMouseButtonDown(int x, int y)
        {
            if (!_isActive || _isPaused)
                return;

            _dispatcher.Invoke(new Action(() => HandleMouseClickOnUiThread(x, y)));
        }

        private void HandleMouseClickOnUiThread(int x, int y)
        {
            if (!_isActive || _isPaused)
                return;

            ShowProcessingOverlay();
            _dispatcher.Invoke(() => { }, DispatcherPriority.Render);

            IndicateCaptureResult capture;
            try
            {
                var detect = IeElementDetector.DetectAtPoint(x, y, forClick: true);
                if (detect?.Element == null)
                {
                    DismissProcessingOverlayCore();
                    var hint = IeElementDetector.GetHintMessage(x, y);
                    if (!string.IsNullOrEmpty(hint))
                    {
                        MessageBox.Show(
                            hint + Environment.NewLine + Environment.NewLine + "按 ESC 可退出 Indicate 模式。",
                            "IE Inspector",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }

                    return;
                }

                capture = IndicateCaptureService.Capture(detect.Controller, detect.Element, detect.FramePath);
            }
            catch
            {
                DismissProcessingOverlayCore();
                return;
            }

            if (capture == null)
            {
                DismissProcessingOverlayCore();
                return;
            }

            Stop(preserveProcessingOverlay: true);
            ElementSelected?.Invoke(capture);
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
                _hotKeyWindow?.Unregister();
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

            if (!_isActive)
                return;

            _isPaused = false;
            _inputHook.ConsumeMouseClick = true;
            _lastMoveTime = DateTime.MinValue;
            _dispatcher.Invoke(() => _hotKeyWindow?.TryRegister());
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
