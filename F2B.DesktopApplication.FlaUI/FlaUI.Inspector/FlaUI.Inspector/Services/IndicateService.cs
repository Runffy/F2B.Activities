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
        private bool _isPaused;
        private bool _disposed;
        private const int DwellMilliseconds = 100;
        private const int PauseSeconds = 3;

        public event Action<IndicateCaptureResult> ElementSelected;
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
            _isPaused = false;
            _lastMoveTime = DateTime.MinValue;

            _dispatcher.Invoke(() =>
            {
                _overlay = new IndicateOverlay();
                _f2HotKeyWindow = new F2HotKeyWindow();
                _f2HotKeyWindow.F2Pressed += OnF2Pressed;
                if (!_f2HotKeyWindow.TryRegister())
                    System.Diagnostics.Debug.WriteLine("FlaUI.Inspector: failed to register F2 hotkey.");
            });

            _inputHook.ConsumeMouseClick = true;
            _inputHook.MouseMoved += OnMouseMoved;
            _inputHook.MouseButtonDown += OnMouseButtonDown;
            _inputHook.Start();
        }

        public void Stop()
        {
            if (!_isActive)
                return;

            _isActive = false;
            _isPaused = false;
            _pauseCts?.Cancel();

            _inputHook.ConsumeMouseClick = false;
            _inputHook.MouseMoved -= OnMouseMoved;
            _inputHook.MouseButtonDown -= OnMouseButtonDown;
            _inputHook.Stop();

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

            _dispatcher.Invoke(new Action(() => HandleMouseClickOnUiThread(x, y)));
        }

        private void HandleMouseClickOnUiThread(int x, int y)
        {
            if (!_isActive || _isPaused)
                return;

            var element = _detector.DetectAtPoint(x, y);
            if (element == null)
                return;

            IndicateCaptureResult capture;
            try
            {
                capture = IndicateCaptureService.Capture(element);
            }
            catch
            {
                return;
            }

            if (capture == null)
                return;

            Stop();
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
                _f2HotKeyWindow?.Unregister();
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
