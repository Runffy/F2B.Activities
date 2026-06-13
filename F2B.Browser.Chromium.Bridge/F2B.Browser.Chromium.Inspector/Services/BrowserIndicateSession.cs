using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using F2B.Browser.Chromium.Bridge;
using F2B.Browser.Chromium.Inspector.Models;
using F2B.Browser.Chromium.Inspector.Overlays;

namespace F2B.Browser.Chromium.Inspector.Services
{
    internal sealed class BrowserIndicateSession : IDisposable
    {
        private const int DwellMilliseconds = 100;
        private const int PauseSeconds = 3;
        private const int ContextMonitorIntervalMs = 300;

        private readonly GlobalInputHook _inputHook = new GlobalInputHook();
        private readonly SemaphoreSlim _rebindLock = new SemaphoreSlim(1, 1);
        private readonly Dispatcher _dispatcher;

        private BridgeInspectorSession _session;
        private BwTab _tab;
        private IndicateHotKeyHandler _hotKeyHandler;
        private IndicateOverlay _indicateOverlay;
        private CountdownOverlay _countdownOverlay;
        private CancellationTokenSource _monitorCts;
        private CancellationTokenSource _pauseCts;
        private TaskCompletionSource<BridgeInspectorPickResult> _pickTcs;
        private DateTime _lastMoveTime = DateTime.MinValue;
        private System.Drawing.Point _lastPoint = System.Drawing.Point.Empty;
        private bool _isActive;
        private bool _isPaused;
        private bool _completed;
        private bool _disposed;
        private int _baselineTabId;
        private string _baselineUrl = string.Empty;

        public event Action<BwTab> TargetTabRebound;

        public BrowserIndicateSession(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public async Task<BridgeInspectorPickResult> RunAsync(BwTab tab, Window ownerWindow, BridgeInspectorSession session)
        {
            if (tab == null)
                throw new ArgumentNullException(nameof(tab));
            if (ownerWindow == null)
                throw new ArgumentNullException(nameof(ownerWindow));
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            _session = session;
            _tab = tab;
            _pickTcs = new TaskCompletionSource<BridgeInspectorPickResult>();
            _completed = false;
            _isActive = true;
            _isPaused = false;
            _lastMoveTime = DateTime.MinValue;

            await Task.Run(() => BootstrapPickAssist(tab, paused: false)).ConfigureAwait(true);

            StartContextMonitor();
            _dispatcher.Invoke(() =>
            {
                _indicateOverlay = new IndicateOverlay();
                _hotKeyHandler = new IndicateHotKeyHandler(ownerWindow);
                _hotKeyHandler.F2Pressed += OnF2Pressed;
                _hotKeyHandler.EscapePressed += OnEscapePressed;
                if (!_hotKeyHandler.TryRegister())
                    System.Diagnostics.Debug.WriteLine("F2B Chromium Inspector: failed to register indicate hotkeys.");
            });

            _inputHook.ConsumeMouseClick = true;
            _inputHook.MouseMoved += OnMouseMoved;
            _inputHook.MouseButtonDown += OnMouseButtonDown;
            _inputHook.Start();

            try
            {
                return await _pickTcs.Task.ConfigureAwait(true);
            }
            finally
            {
                StopCore();
            }
        }

        private void BootstrapPickAssist(BwTab tab, bool paused)
        {
            if (tab == null)
                return;

            tab.Activate();
            tab.InspectorRestartPickAssist();
            CaptureBaseline(tab);

            if (paused)
                tab.InspectorPausePickAssist();
        }

        private void CaptureBaseline(BwTab tab)
        {
            if (tab == null)
                return;

            var info = tab.GetInfo();
            _baselineTabId = tab.TabId;
            _baselineUrl = NormalizeUrl(info?.Url ?? tab.Url);
        }

        private void StartContextMonitor()
        {
            _monitorCts?.Cancel();
            _monitorCts = new CancellationTokenSource();
            _ = MonitorIndicateContextAsync(_monitorCts.Token);
        }

        private async Task MonitorIndicateContextAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(ContextMonitorIntervalMs, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (!_isActive || _completed)
                    return;

                var tab = _tab;
                var session = _session;
                if (tab == null || session == null)
                    return;

                try
                {
                    var change = await Task.Run(() => DetectContextChange(tab, session), token).ConfigureAwait(false);
                    if (change == ContextChange.None)
                        continue;

                    if (change == ContextChange.TabClosed)
                    {
                        CompleteInvalidated(IndicateInvalidatedReason.TabClosed);
                        return;
                    }

                    if (change == ContextChange.RestrictedTab)
                    {
                        CompleteInvalidated(IndicateInvalidatedReason.RestrictedTab);
                        return;
                    }

                    await RebindContextAsync(change, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch
                {
                }
            }
        }

        private enum ContextChange
        {
            None,
            TabClosed,
            RestrictedTab,
            TabSwitched,
            PageNavigated
        }

        private ContextChange DetectContextChange(BwTab tab, BridgeInspectorSession session)
        {
            var info = tab.GetInfo();
            if (info != null && info.IsClosed)
                return ContextChange.TabClosed;

            var client = session.GetClient(tab.InstanceId);
            var activeTab = client.GetBrowser().GetActivatedTab();
            if (activeTab == null)
                return ContextChange.None;

            var activeInfo = activeTab.GetInfo();
            var activeUrl = activeInfo?.Url ?? activeTab.Url;
            if (!BridgeUrlRules.IsInjectableUrl(activeUrl))
            {
                if (activeTab.TabId != _baselineTabId)
                    return ContextChange.RestrictedTab;

                return ContextChange.None;
            }

            if (activeTab.TabId != _baselineTabId)
                return ContextChange.TabSwitched;

            var currentUrl = NormalizeUrl(info?.Url);
            if (!string.IsNullOrEmpty(_baselineUrl) &&
                !string.IsNullOrEmpty(currentUrl) &&
                !string.Equals(_baselineUrl, currentUrl, StringComparison.OrdinalIgnoreCase))
            {
                return ContextChange.PageNavigated;
            }

            return ContextChange.None;
        }

        private async Task RebindContextAsync(ContextChange change, CancellationToken token)
        {
            await _rebindLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (!_isActive || _completed)
                    return;

                var session = _session;
                if (session == null)
                    return;

                var paused = _isPaused;
                BwTab reboundTab = null;

                await Task.Run(() =>
                {
                    if (change == ContextChange.TabSwitched)
                    {
                        var client = session.GetClient(_tab.InstanceId);
                        var activeTab = client.GetBrowser().GetActivatedTab();
                        if (activeTab == null)
                            return;

                        TryStopPickAssist(_tab);
                        _tab = activeTab;
                        reboundTab = activeTab;
                    }
                    else
                    {
                        reboundTab = _tab;
                    }

                    if (reboundTab == null)
                        return;

                    BootstrapPickAssist(reboundTab, paused);
                }, token).ConfigureAwait(false);

                if (reboundTab == null)
                    return;

                _lastMoveTime = DateTime.MinValue;
                _dispatcher.Invoke(() => _indicateOverlay?.HideHighlight());

                if (change == ContextChange.TabSwitched)
                    TargetTabRebound?.Invoke(reboundTab);
            }
            finally
            {
                _rebindLock.Release();
            }
        }

        private static void TryStopPickAssist(BwTab tab)
        {
            if (tab == null)
                return;

            try
            {
                tab.InspectorStopPickAssist();
            }
            catch
            {
            }
        }

        private static string NormalizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return string.Empty;

            return url.Trim();
        }

        private void CompleteInvalidated(string reason)
        {
            Complete(new BridgeInspectorPickResult
            {
                Cancelled = true,
                InvalidatedReason = reason
            });
        }

        private void OnEscapePressed()
        {
            Complete(new BridgeInspectorPickResult { Cancelled = true });
        }

        private void OnF2Pressed()
        {
            if (!_isActive || _isPaused)
                return;

            _ = BeginPauseCountdownAsync();
        }

        private void OnMouseMoved(int x, int y)
        {
            if (!_isActive || _isPaused)
                return;

            _lastPoint = new System.Drawing.Point(x, y);
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

                var tab = _tab;
                if (tab == null)
                    return;

                try
                {
                    var hover = tab.InspectorHoverAtScreenPoint(capturedPoint.X, capturedPoint.Y);
                    var highlightBounds = hover != null && hover.Hovered && hover.Bounds.HasValue
                        ? (Rectangle?)hover.Bounds.Value
                        : null;
                    _dispatcher.Invoke(new Action(() =>
                    {
                        if (!_isActive || _isPaused)
                            return;

                        if (highlightBounds.HasValue)
                            _indicateOverlay?.UpdateHighlight(highlightBounds.Value);
                        else
                            _indicateOverlay?.HideHighlight();
                    }));
                }
                catch
                {
                }
            });
        }

        private void OnMouseButtonDown(int x, int y)
        {
            if (!_isActive || _isPaused)
                return;

            Task.Run(() =>
            {
                var tab = _tab;
                if (tab == null)
                    return;

                try
                {
                    var pick = tab.InspectorPickAtScreenPoint(x, y);
                    if (pick == null || pick.Cancelled)
                        return;

                    if (pick.Segments == null || pick.Segments.Length == 0)
                        return;

                    Complete(pick);
                }
                catch
                {
                }
            });
        }

        private async Task BeginPauseCountdownAsync()
        {
            _isPaused = true;
            _inputHook.ConsumeMouseClick = false;
            _pauseCts?.Cancel();
            _pauseCts = new CancellationTokenSource();
            var token = _pauseCts.Token;

            try
            {
                await Task.Run(() => _tab?.InspectorPausePickAssist(), token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            _dispatcher.Invoke(() =>
            {
                _hotKeyHandler?.Unregister();
                _indicateOverlay?.HideHighlight();
                _countdownOverlay?.Close();
                _countdownOverlay = new CountdownOverlay();
                _countdownOverlay.ShowCountdown(PauseSeconds);
            });

            try
            {
                for (var i = PauseSeconds; i >= 1; i--)
                {
                    var value = i;
                    _dispatcher.Invoke(() => _countdownOverlay?.UpdateCount(value));
                    await Task.Delay(1000, token).ConfigureAwait(false);
                }

                await Task.Run(() => BootstrapPickAssist(_tab, paused: false), token).ConfigureAwait(false);

                _dispatcher.Invoke(() =>
                {
                    _countdownOverlay?.Close();
                    _countdownOverlay = null;

                    if (_isActive)
                        _hotKeyHandler?.TryRegister();
                });

                _isPaused = false;
                _inputHook.ConsumeMouseClick = true;
                _lastMoveTime = DateTime.MinValue;
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void Complete(BridgeInspectorPickResult result)
        {
            if (_completed)
                return;

            _completed = true;
            _pickTcs?.TrySetResult(result ?? new BridgeInspectorPickResult { Cancelled = true });
        }

        private void StopCore()
        {
            if (!_isActive)
                return;

            _isActive = false;
            _isPaused = false;
            _pauseCts?.Cancel();
            _monitorCts?.Cancel();

            _session = null;
            _inputHook.ConsumeMouseClick = false;
            _inputHook.MouseMoved -= OnMouseMoved;
            _inputHook.MouseButtonDown -= OnMouseButtonDown;
            _inputHook.Stop();

            _dispatcher.Invoke(CloseOverlays);

            var tab = _tab;
            _tab = null;
            TryStopPickAssist(tab);
        }

        private void CloseOverlays()
        {
            _indicateOverlay?.HideHighlight();
            _indicateOverlay?.Close();
            _indicateOverlay = null;

            _countdownOverlay?.Close();
            _countdownOverlay = null;

            if (_hotKeyHandler != null)
            {
                _hotKeyHandler.F2Pressed -= OnF2Pressed;
                _hotKeyHandler.EscapePressed -= OnEscapePressed;
                _hotKeyHandler.Dispose();
                _hotKeyHandler = null;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            if (!_completed)
                _pickTcs?.TrySetResult(new BridgeInspectorPickResult { Cancelled = true });

            StopCore();
            _inputHook.Dispose();
            _rebindLock.Dispose();
            _disposed = true;
        }
    }
}
