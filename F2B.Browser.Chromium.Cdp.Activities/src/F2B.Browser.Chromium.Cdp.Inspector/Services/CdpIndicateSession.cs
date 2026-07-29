using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using F2B.Browser.Chromium.Cdp.Browser;
using F2B.Browser.Chromium.Cdp.Inspector.Overlays;

namespace F2B.Browser.Chromium.Cdp.Inspector.Services
{
    /// <summary>
    /// Indicate session: mouse-hit browser discovery, sticky CDP attach for the session, F2/Esc.
    /// </summary>
    internal sealed class CdpIndicateSession : IDisposable
    {
        private const int HoverDebounceMs = 80;
        private const int MinHoverRpcIntervalMs = 50;
        private const int PauseSeconds = 3;
        private const string NoDebugPortMessage = "该浏览器未使用 remote debugging port 打开";

        private readonly GlobalInputHook _inputHook = new GlobalInputHook();
        private readonly object _hoverPointSync = new object();
        private readonly Dispatcher _dispatcher;

        private IndicateHotKeyHandler _hotKeyHandler;
        private CountdownOverlay _countdownOverlay;
        private CursorBubbleOverlay _bubbleOverlay;
        private CancellationTokenSource _pauseCts;
        private CancellationTokenSource _hoverDebounceCts;
        private TaskCompletionSource<CdpIndicatePickResult> _pickTcs;
        private DateTime _lastHoverRpcUtc = DateTime.MinValue;
        private System.Drawing.Point _lastPoint = System.Drawing.Point.Empty;
        private int _hoverInFlight;
        private bool _isActive;
        private bool _isPaused;
        private bool _completed;
        private bool _disposed;

        private CdpBrowser _browser;
        private CdpTab _tab;
        private CdpDiscoveredBrowser _discovered;
        private int _attachedPort = -1;
        private int _cachedPid = -1;
        private BrowserFromPoint.HitResult _cachedHit;

        public CdpIndicateSession(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public async Task<CdpIndicatePickResult> RunAsync(Window ownerWindow)
        {
            if (ownerWindow == null)
                throw new ArgumentNullException(nameof(ownerWindow));

            _pickTcs = new TaskCompletionSource<CdpIndicatePickResult>();
            _completed = false;
            _isActive = true;
            _isPaused = false;
            _lastHoverRpcUtc = DateTime.MinValue;

            _dispatcher.Invoke(() =>
            {
                _hotKeyHandler = new IndicateHotKeyHandler(ownerWindow);
                _hotKeyHandler.F2Pressed += OnF2Pressed;
                _hotKeyHandler.EscapePressed += OnEscapePressed;
                if (!_hotKeyHandler.TryRegister())
                    System.Diagnostics.Debug.WriteLine("F2B CDP Inspector: failed to register indicate hotkeys.");

                _bubbleOverlay = new CursorBubbleOverlay();
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

        private void OnEscapePressed()
        {
            Complete(new CdpIndicatePickResult { Cancelled = true });
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

            lock (_hoverPointSync)
            {
                _lastPoint = new System.Drawing.Point(x, y);
            }

            ScheduleHoverRpc();
        }

        private void ScheduleHoverRpc()
        {
            _hoverDebounceCts?.Cancel();
            _hoverDebounceCts?.Dispose();
            _hoverDebounceCts = new CancellationTokenSource();
            var token = _hoverDebounceCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(HoverDebounceMs, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (!_isActive || _isPaused)
                    return;

                if (Interlocked.CompareExchange(ref _hoverInFlight, 1, 0) != 0)
                    return;

                try
                {
                    var elapsed = (DateTime.UtcNow - _lastHoverRpcUtc).TotalMilliseconds;
                    if (elapsed < MinHoverRpcIntervalMs)
                        await Task.Delay((int)(MinHoverRpcIntervalMs - elapsed), token).ConfigureAwait(false);

                    System.Drawing.Point point;
                    lock (_hoverPointSync)
                    {
                        point = _lastPoint;
                    }

                    HandleHover(point.X, point.Y);
                    _lastHoverRpcUtc = DateTime.UtcNow;
                }
                catch (OperationCanceledException)
                {
                }
                catch
                {
                }
                finally
                {
                    Interlocked.Exchange(ref _hoverInFlight, 0);
                }
            }, token);
        }

        private void HandleHover(int x, int y)
        {
            var hit = ResolveHitCached(x, y);
            if (hit.Kind == BrowserFromPoint.HitKind.None)
            {
                HideBubble();
                ClearPageHover();
                // Keep CDP attached for the whole Indicate session — do not disconnect on leave.
                return;
            }

            if (hit.Kind == BrowserFromPoint.HitKind.ChromiumWithoutDebugPort)
            {
                ClearPageHover();
                ShowBubble(NoDebugPortMessage);
                return;
            }

            HideBubble();
            EnsureAttached(hit.Browser);

            // Sticky tab: reuse last tab if hover still hits; otherwise find once.
            string hint;
            if (_tab != null && CdpPagePickAssist.TryHover(_tab, x, y, out hint))
                return;

            var tab = FindTabUnderPoint(x, y);
            if (tab == null)
            {
                ClearPageHover();
                return;
            }

            _tab = tab;
            CdpPagePickAssist.TryHover(tab, x, y, out hint);
        }

        private void OnMouseButtonDown(int x, int y)
        {
            if (!_isActive || _isPaused)
                return;

            _hoverDebounceCts?.Cancel();

            Task.Run(() =>
            {
                try
                {
                    var hit = ResolveHitCached(x, y);
                    if (hit.Kind == BrowserFromPoint.HitKind.ChromiumWithoutDebugPort)
                    {
                        ShowBubble(NoDebugPortMessage);
                        return;
                    }

                    if (hit.Kind != BrowserFromPoint.HitKind.DebuggableBrowser)
                        return;

                    EnsureAttached(hit.Browser);
                    var tab = _tab;
                    string hint;
                    if (tab == null || !CdpPagePickAssist.TryHover(tab, x, y, out hint))
                        tab = FindTabUnderPoint(x, y) ?? _browser?.LatestTab;

                    if (tab == null)
                        return;

                    _tab = tab;
                    var pick = CdpPagePickAssist.PickAndBuild(
                        tab,
                        x,
                        y,
                        hit.Browser.BrowserName,
                        hit.Browser.Port);
                    if (pick == null || pick.Cancelled)
                        return;

                    if (pick.Levels == null || pick.Levels.Count == 0)
                        return;

                    Complete(pick);
                }
                catch
                {
                }
            });
        }

        private BrowserFromPoint.HitResult ResolveHitCached(int x, int y)
        {
            var quick = BrowserFromPoint.ResolveWindowProcess(x, y);
            if (quick.ProcessId > 0 && quick.ProcessId == _cachedPid && _cachedHit != null)
                return _cachedHit;

            var hit = BrowserFromPoint.Resolve(x, y);
            if (hit.ProcessId > 0)
            {
                _cachedPid = hit.ProcessId;
                _cachedHit = hit;
            }

            return hit;
        }

        private void EnsureAttached(CdpDiscoveredBrowser discovered)
        {
            if (discovered == null)
                return;

            if (_browser != null && _attachedPort == discovered.Port)
            {
                _discovered = discovered;
                return;
            }

            DisconnectBrowser();
            _discovered = discovered;
            _attachedPort = discovered.Port;
            _browser = CdpBrowser.Attach(discovered.Port);
            _tab = null;
        }

        private CdpTab FindTabUnderPoint(int screenX, int screenY)
        {
            if (_browser == null)
                return null;

            try
            {
                // Prefer already-open tab session; LatestTab first (usually the foreground one).
                var latest = _browser.LatestTab;
                string hint;
                if (latest != null && CdpPagePickAssist.TryHover(latest, screenX, screenY, out hint))
                    return latest;

                foreach (var tab in _browser.GetTabs())
                {
                    if (latest != null && string.Equals(tab.Id, latest.Id, StringComparison.Ordinal))
                        continue;

                    if (CdpPagePickAssist.TryHover(tab, screenX, screenY, out hint))
                        return tab;
                }

                return latest;
            }
            catch
            {
                return null;
            }
        }

        private void ClearPageHover()
        {
            try
            {
                CdpPagePickAssist.ClearHover(_tab);
            }
            catch
            {
            }
        }

        private void DisconnectBrowser()
        {
            ClearPageHover();
            _tab = null;
            if (_browser != null)
            {
                CdpShortLivedResolver.DisposeQuietly(_browser);
                _browser = null;
            }

            _attachedPort = -1;
            _discovered = null;
        }

        private void ShowBubble(string message)
        {
            _dispatcher.BeginInvoke(new Action(() =>
            {
                if (_bubbleOverlay == null)
                    _bubbleOverlay = new CursorBubbleOverlay();
                _bubbleOverlay.ShowNearCursor(message);
            }));
        }

        private void HideBubble()
        {
            _dispatcher.BeginInvoke(new Action(() => _bubbleOverlay?.Hide()));
        }

        private async Task BeginPauseCountdownAsync()
        {
            _isPaused = true;
            _inputHook.ConsumeMouseClick = false;
            _hoverDebounceCts?.Cancel();
            ClearPageHover();
            HideBubble();
            _pauseCts?.Cancel();
            _pauseCts = new CancellationTokenSource();
            var token = _pauseCts.Token;

            _dispatcher.Invoke(() =>
            {
                _hotKeyHandler?.Unregister();
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

                _dispatcher.Invoke(() =>
                {
                    _countdownOverlay?.Close();
                    _countdownOverlay = null;
                    if (_isActive)
                        _hotKeyHandler?.TryRegister();
                });

                _isPaused = false;
                _inputHook.ConsumeMouseClick = true;
                _lastHoverRpcUtc = DateTime.MinValue;
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void Complete(CdpIndicatePickResult result)
        {
            if (_completed)
                return;

            _completed = true;
            _pickTcs?.TrySetResult(result ?? new CdpIndicatePickResult { Cancelled = true });
        }

        private void StopCore()
        {
            if (!_isActive)
                return;

            _isActive = false;
            _isPaused = false;
            _pauseCts?.Cancel();
            _hoverDebounceCts?.Cancel();
            _hoverDebounceCts?.Dispose();
            _hoverDebounceCts = null;

            _inputHook.ConsumeMouseClick = false;
            _inputHook.MouseMoved -= OnMouseMoved;
            _inputHook.MouseButtonDown -= OnMouseButtonDown;
            _inputHook.Stop();

            DisconnectBrowser();
            _dispatcher.Invoke(CloseOverlays);
        }

        private void CloseOverlays()
        {
            _countdownOverlay?.Close();
            _countdownOverlay = null;
            _bubbleOverlay?.Close();
            _bubbleOverlay = null;

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
                _pickTcs?.TrySetResult(new CdpIndicatePickResult { Cancelled = true });

            StopCore();
            _inputHook.Dispose();
            _disposed = true;
        }
    }
}
