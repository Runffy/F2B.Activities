using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using F2B.Browser.Chromium.Bridge;
using F2B.Browser.Chromium.Bridge.Selectors;
using F2B.Browser.Chromium.Inspector.Helpers;
using F2B.Browser.Chromium.Inspector.Models;
using F2B.Browser.Chromium.Inspector.Overlays;
using F2B.Browser.Chromium.Inspector.Services;

namespace F2B.Browser.Chromium.Inspector.ViewModels
{
    public sealed class MainViewModel : NotifyObject
    {
        private static readonly SolidColorBrush GrayBackground = new SolidColorBrush(Color.FromRgb(232, 232, 232));
        private static readonly SolidColorBrush GrayBorder = new SolidColorBrush(Color.FromRgb(189, 189, 189));
        private static readonly SolidColorBrush GrayForeground = new SolidColorBrush(Color.FromRgb(51, 51, 51));
        private static readonly SolidColorBrush GreenBackground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
        private static readonly SolidColorBrush GreenBorder = new SolidColorBrush(Color.FromRgb(56, 142, 60));
        private static readonly SolidColorBrush OrangeBackground = new SolidColorBrush(Color.FromRgb(255, 152, 0));
        private static readonly SolidColorBrush OrangeBorder = new SolidColorBrush(Color.FromRgb(230, 126, 34));
        private static readonly SolidColorBrush RedBackground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
        private static readonly SolidColorBrush RedBorder = new SolidColorBrush(Color.FromRgb(211, 47, 47));
        private static readonly SolidColorBrush RedForeground = new SolidColorBrush(Color.FromRgb(211, 47, 47));
        private static readonly SolidColorBrush TargetElementNormalForeground = new SolidColorBrush(Color.FromRgb(51, 51, 51));
        private static readonly SolidColorBrush WhiteForeground = new SolidColorBrush(Colors.White);

        private const int HighlightDurationMs = 3000;
        private const int ResolveTimeoutMs = SelectorResolveRetry.DefaultTimeoutMilliseconds;
        private const int ResolveIntervalMs = SelectorResolveRetry.DefaultIntervalMilliseconds;
        private const int PostCaptureValidateDelayMs = 300;

        private readonly BridgeInspectorSession _session = new BridgeInspectorSession();
        private CancellationTokenSource _postCaptureCts;
        private Dispatcher _dispatcher;
        private BwTab _targetTab;
        private InspectorSelectorLevel _selectedSelectorLevel;
        private string _selectorXml = string.Empty;
        private string _targetElementDisplay = string.Empty;
        private string _connectionStatus = "Starting bridge server...";
        private ValidationState _validationState = ValidationState.None;
        private bool _hasCapturedElement;
        private bool _isIndicating;
        private bool _isHighlighting;
        private bool _isValidating;
        private bool _isTargetElementError;
        private bool _suppressTargetElementStatusMessage;
        private bool _suppressSelectorUpdate;
        private AnalyzingOverlay _analyzingOverlay;

        public MainViewModel()
        {
            SelectorLevels = new ObservableCollection<InspectorSelectorLevel>();
            SelectedItemProperties = new ObservableCollection<InspectorPropertyItem>();

            RefreshConnectionCommand = new RelayCommand(ReconnectAndRefreshStatus);
            IndicateCommand = new RelayCommand(() => _ = StartIndicateAsync(), () => _session.IsConnected && !IsIndicating && !IsHighlighting && !IsValidating);
            ValidateCommand = new RelayCommand(() => _ = ValidateSelectorAsync(), () => _hasCapturedElement && SelectorLevels.Count > 0 && !IsValidating && !IsHighlighting);
            HighlightCommand = new RelayCommand(() => _ = HighlightAsync(), () => !IsHighlighting && !IsIndicating && !IsValidating && SelectorLevels.Count > 0);
        }

        public ObservableCollection<InspectorSelectorLevel> SelectorLevels { get; }
        public ObservableCollection<InspectorPropertyItem> SelectedItemProperties { get; }

        public RelayCommand RefreshConnectionCommand { get; }
        public RelayCommand IndicateCommand { get; }
        public RelayCommand ValidateCommand { get; }
        public RelayCommand HighlightCommand { get; }

        public string ConnectionStatus
        {
            get => _connectionStatus;
            private set => SetProperty(ref _connectionStatus, value);
        }

        public string TargetElementDisplay
        {
            get => _targetElementDisplay;
            private set => SetProperty(ref _targetElementDisplay, value);
        }

        public Brush TargetElementForeground =>
            _isTargetElementError ? RedForeground : TargetElementNormalForeground;

        public InspectorSelectorLevel SelectedSelectorLevel
        {
            get => _selectedSelectorLevel;
            set
            {
                if (!SetProperty(ref _selectedSelectorLevel, value))
                    return;

                LoadSelectedItemProperties(value);
            }
        }

        public string SelectorXml
        {
            get => _selectorXml;
            set
            {
                if (!SetProperty(ref _selectorXml, value))
                    return;

                if (!_suppressSelectorUpdate)
                    MarkValidationStale();
            }
        }

        public ValidationState ValidationState
        {
            get => _validationState;
            private set
            {
                if (SetProperty(ref _validationState, value))
                {
                    RaisePropertyChanged(nameof(ValidateButtonBackground));
                    RaisePropertyChanged(nameof(ValidateButtonBorderBrush));
                    RaisePropertyChanged(nameof(ValidateButtonForeground));
                }
            }
        }

        public Brush ValidateButtonBackground => GetValidateBrush(true);
        public Brush ValidateButtonBorderBrush => GetValidateBrush(false);
        public Brush ValidateButtonForeground =>
            ValidationState == ValidationState.None ? GrayForeground : WhiteForeground;

        public bool IsIndicating
        {
            get => _isIndicating;
            private set
            {
                if (SetProperty(ref _isIndicating, value))
                    CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool IsHighlighting
        {
            get => _isHighlighting;
            private set
            {
                if (SetProperty(ref _isHighlighting, value))
                    CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool IsValidating
        {
            get => _isValidating;
            private set
            {
                if (SetProperty(ref _isValidating, value))
                    CommandManager.InvalidateRequerySuggested();
            }
        }


        public void Initialize(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            _session.ClientsChanged += (_, __) => _dispatcher.BeginInvoke(new Action(RefreshConnectionStatus));

            try
            {
                _session.Start();
                RefreshConnectionStatus();
            }
            catch (Exception ex)
            {
                ConnectionStatus = "Bridge connection failed: " + ex.Message;
            }
        }

        public void Cleanup()
        {
            _session.ClientsChanged -= (_, __) => _dispatcher?.BeginInvoke(new Action(RefreshConnectionStatus));
            _session.Dispose();
        }

        public void RefreshConnectionStatus()
        {
            UpdateConnectionStatusMessage();
        }

        public void ReconnectAndRefreshStatus()
        {
            try
            {
                ConnectionStatus = "Reconnecting to Bridge on port " + BridgeConstants.DefaultPort + "...";
                _postCaptureCts?.Cancel();
                _session.Reconnect();
            }
            catch (Exception ex)
            {
                ConnectionStatus = "Bridge connection failed: " + ex.Message;
                return;
            }

            if (IsBridgeDisconnectMessage(TargetElementDisplay))
                SetTargetElementError(false);

            UpdateConnectionStatusMessage();
        }

        private void UpdateConnectionStatusMessage()
        {
            if (!_session.IsConnected)
            {
                ConnectionStatus = _session.Mode == BridgeSessionMode.Attached
                    ? "Attached to shared Bridge on port " + BridgeConstants.DefaultPort +
                      " (host started by OpenRPA or Inspector). Waiting for Chrome extension..."
                    : "Bridge host running on port " + BridgeConstants.DefaultPort +
                      ". Waiting for Chrome extension...";
                return;
            }

            var modeText = _session.Mode == BridgeSessionMode.Attached
                ? "Attached to shared Bridge"
                : "Bridge host running";

            ConnectionStatus = modeText + " (" + _session.ConnectedCount +
                               " extension). Activate the target tab in Chrome, then click Indicate Element.";
        }

        private async Task StartIndicateAsync()
        {
            IsIndicating = true;
            ValidationState = ValidationState.None;

            var window = Application.Current?.MainWindow;
            if (window != null)
                window.WindowState = WindowState.Minimized;

            BrowserIndicateSession session = null;
            try
            {
                var tab = await Task.Run(() => _session.GetTargetTab());
                _targetTab = tab;

                session = new BrowserIndicateSession(_dispatcher);
                session.TargetTabRebound += OnIndicateTargetTabRebound;
                var pick = await session.RunAsync(tab, window, _session);
                if (pick.Cancelled)
                {
                    if (!string.IsNullOrEmpty(pick.InvalidatedReason))
                    {
                        SetTargetElementError(true);
                        SetTargetElementStatusMessage(FormatIndicateInvalidated(pick.InvalidatedReason));
                    }
                    else
                    {
                        SetTargetElementError(false);
                        SetTargetElementStatusMessage(pick.RestrictedUrl
                            ? "Indicate skipped: the active tab is a restricted Chrome page (for example chrome://extensions). Switch to a normal http/https page and try again."
                            : "Pick cancelled.");
                    }

                    return;
                }

                ShowAnalyzingOverlay();
                _suppressTargetElementStatusMessage = true;

                if (pick.Levels != null && pick.Levels.Count > 0)
                {
                    await ApplyCaptureAfterIndicateAsync(pick.Levels, pick.Segments, pick.DisplayName);
                }
                else
                {
                    var build = await Task.Run(() => tab.InspectorBuildSelector(pick.Segments));
                    await ApplyCaptureAfterIndicateAsync(build.Levels, build.Segments, build.DisplayName, build.MinimalLevels);
                }
            }
            catch (Exception ex)
            {
                _suppressTargetElementStatusMessage = false;
                SetTargetElementError(true);
                SetTargetElementStatusMessage(FormatIndicateError(ex));
            }
            finally
            {
                _suppressTargetElementStatusMessage = false;

                if (session != null)
                    session.TargetTabRebound -= OnIndicateTargetTabRebound;

                session?.Dispose();
                HideAnalyzingOverlay();

                if (window != null && _dispatcher != null)
                {
                    await _dispatcher.InvokeAsync(() => WindowRestoreHelper.RestoreAfterIndicateComplete(window));
                }
                else if (window != null)
                {
                    window.WindowState = WindowState.Normal;
                }

                IsIndicating = false;
            }
        }

        private void ShowAnalyzingOverlay()
        {
            if (_dispatcher == null)
                return;

            _dispatcher.Invoke(() =>
            {
                _analyzingOverlay?.Close();
                _analyzingOverlay = new AnalyzingOverlay();
                _analyzingOverlay.ShowMessage("正在分析中...");
            });
        }

        private void HideAnalyzingOverlay()
        {
            if (_dispatcher == null)
                return;

            _dispatcher.Invoke(() =>
            {
                _analyzingOverlay?.Close();
                _analyzingOverlay = null;
            });
        }

        private async Task ApplyCaptureAfterIndicateAsync(
            IList<SelectorLevel> levels,
            object[] segments,
            string displayName,
            IList<SelectorLevel> minimalLevels = null)
        {
            ApplyCapture(levels, segments, displayName, minimalLevels, runPostCaptureValidation: false);
            await Task.Delay(PostCaptureValidateDelayMs).ConfigureAwait(true);
            await ValidateSelectorAsync().ConfigureAwait(true);
        }

        private void OnIndicateTargetTabRebound(BwTab tab)
        {
            if (tab == null || _dispatcher == null)
                return;

            if (_dispatcher.CheckAccess())
                _targetTab = tab;
            else
                _dispatcher.Invoke(() => _targetTab = tab);
        }

        private void ApplyCapture(
            IList<SelectorLevel> levels,
            object[] segments,
            string displayName,
            IList<SelectorLevel> minimalLevels = null,
            bool runPostCaptureValidation = true)
        {
            SetTargetElementError(false);
            _hasCapturedElement = levels != null && levels.Count > 0;
            SetCapturedElementDisplayName(displayName ?? string.Empty);

            SelectorLevels.Clear();
            SelectedItemProperties.Clear();

            foreach (var level in InspectorSelectorSerializer.FromBridgeLevels(levels))
            {
                AttachSelectorHandlers(level);
                SelectorLevels.Add(level);
            }

            SelectedSelectorLevel = SelectorLevels.LastOrDefault();

            if (minimalLevels != null && minimalLevels.Count > 0)
            {
                _suppressSelectorUpdate = true;
                SelectorXml = SelectorXmlSerializer.Serialize(minimalLevels);
                _suppressSelectorUpdate = false;
            }
            else
            {
                UpdateSelectorXmlFromLevels();
            }

            if (!runPostCaptureValidation)
                return;

            _postCaptureCts?.Cancel();
            _postCaptureCts?.Dispose();
            _postCaptureCts = new CancellationTokenSource();
            _ = ValidateAfterCaptureAsync(_postCaptureCts.Token);
        }

        private async Task ValidateAfterCaptureAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(PostCaptureValidateDelayMs, token).ConfigureAwait(true);

                if (!token.IsCancellationRequested)
                    await ValidateSelectorAsync().ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task ValidateSelectorAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectorXml))
                return;

            if (!_session.IsConnected)
            {
                ValidationState = ValidationState.Invalid;
                SetTargetElementError(true);
                return;
            }

            IsValidating = true;
            try
            {
                var scope = SelectorXmlSerializer.SplitScope(SelectorXml);
                if (string.IsNullOrWhiteSpace(SelectorXmlSerializer.ToOperationXml(scope)))
                {
                    ValidationState = ValidationState.Invalid;
                    SetTargetElementError(true);
                    return;
                }

                var result = await CountSelectorMatchesWithRetryAsync().ConfigureAwait(true);
                ApplyValidationResult(result);
            }
            catch (Exception ex)
            {
                ValidationState = ValidationState.Invalid;
                SetTargetElementError(true);
                if (IsBridgeDisconnectError(ex))
                    UpdateConnectionStatusMessage();
            }
            finally
            {
                IsValidating = false;
            }
        }

        private void ApplyValidationResult(SelectorResolveResult result)
        {
            if (result == null)
                result = SelectorResolveResult.None;

            if (result.Count <= 0)
            {
                ValidationState = ValidationState.Invalid;
                SetTargetElementError(true);
                BridgeFileLog.Write("[INSPECTOR-VALIDATE] failed attempts=" + result.Attempts +
                                    " error=" + (result.LastError ?? string.Empty));
                return;
            }

            if (result.Count == 1)
            {
                ValidationState = ValidationState.Valid;
                SetTargetElementError(false);
                BridgeFileLog.Write("[INSPECTOR-VALIDATE] ok matches=1 attempts=" + result.Attempts);
                return;
            }

            ValidationState = ValidationState.Ambiguous;
            SetTargetElementError(true);
        }

        private async Task<SelectorResolveResult> CountSelectorMatchesWithRetryAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectorXml))
                return SelectorResolveResult.None;

            return await SelectorResolveRetry.CountMatchesWithRetryAsync(
                () => Task.Run(() => _session.Host.FindElements(SelectorXml, _targetTab).Length),
                ResolveTimeoutMs,
                ResolveIntervalMs).ConfigureAwait(true);
        }

        private async Task PrepareTargetTabForResolveAsync()
        {
            await _dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

            var tab = _targetTab ?? await Task.Run(() => _session.GetTargetTab());
            tab = await Task.Run(() => _session.RefreshTargetTab(tab));
            _targetTab = tab;
            if (tab != null)
                await Task.Run(() => tab.Activate());
        }

        private static bool IsBridgeDisconnectMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;

            return message.IndexOf("Bridge controller socket is not connected", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("Bridge connection lost", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsBridgeDisconnectError(Exception ex)
        {
            while (ex != null)
            {
                if (IsBridgeDisconnectMessage(ex.Message))
                    return true;

                ex = ex.InnerException;
            }

            return false;
        }

        private async Task HighlightAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectorXml))
                return;

            IsHighlighting = true;
            try
            {
                await PrepareTargetTabForResolveAsync();

                var result = await CountSelectorMatchesWithRetryAsync().ConfigureAwait(true);
                if (result.Count != 1)
                {
                    SetTargetElementError(true);
                    return;
                }

                var tab = _targetTab;
                await Task.Run(() => tab.InspectorHighlight(SelectorXml, HighlightDurationMs));
                await Task.Delay(HighlightDurationMs);
            }
            catch (Exception ex)
            {
                SetTargetElementError(true);
                BridgeFileLog.Write("[INSPECTOR-HIGHLIGHT] " + ex.Message);
            }
            finally
            {
                IsHighlighting = false;
            }
        }

        private void AttachSelectorHandlers(InspectorSelectorLevel level)
        {
            level.PropertyChanged += (_, __) => UpdateSelectorXmlFromLevels();
            foreach (var property in level.Properties)
            {
                property.PropertyChanged += (_, __) =>
                {
                    level.RefreshTagLine();
                    UpdateSelectorXmlFromLevels();
                };
            }
        }

        private void LoadSelectedItemProperties(InspectorSelectorLevel level)
        {
            SelectedItemProperties.Clear();
            if (level == null)
                return;

            foreach (var property in level.Properties
                         .OrderBy(item => item.Value?.Length ?? 0)
                         .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
            {
                SelectedItemProperties.Add(property);
            }
        }

        private void UpdateSelectorXmlFromLevels()
        {
            _suppressSelectorUpdate = true;
            SelectorXml = InspectorSelectorSerializer.Serialize(SelectorLevels);
            _suppressSelectorUpdate = false;
            MarkValidationStale();
        }

        private void MarkValidationStale()
        {
            if (ValidationState != ValidationState.None)
                ValidationState = ValidationState.None;
        }

        public void ReportRuntimeError(Exception exception)
        {
            SetTargetElementError(true);
            SetTargetElementStatusMessage(FormatIndicateError(exception));
        }

        private void SetCapturedElementDisplayName(string displayName)
        {
            SetTargetElementError(false);
            TargetElementDisplay = displayName ?? string.Empty;
        }

        private void SetTargetElementStatusMessage(string message)
        {
            if (_suppressTargetElementStatusMessage)
                return;

            TargetElementDisplay = message ?? string.Empty;
        }

        private static string FormatIndicateInvalidated(string reason)
        {
            switch (reason)
            {
                case IndicateInvalidatedReason.TabClosed:
                    return "Indicate 已取消：目标标签页已关闭。";
                case IndicateInvalidatedReason.RestrictedTab:
                    return "Indicate 已取消：当前标签页为 Chrome 受限页面（如 chrome://）。请切换到普通 http/https 页面后重新 Indicate。";
                default:
                    return "Indicate 已取消：" + reason;
            }
        }

        private void SetTargetElementError(bool isError)
        {
            if (_isTargetElementError == isError)
                return;

            _isTargetElementError = isError;
            RaisePropertyChanged(nameof(TargetElementForeground));
        }

        private static string FormatIndicateError(Exception exception)
        {
            var message = exception is AggregateException aggregate
                ? aggregate.Flatten().InnerException?.Message ?? aggregate.Message
                : exception?.Message;

            if (BridgeUrlRules.IsRestrictedUrlError(message))
            {
                return "Indicate skipped: Chrome blocked access to a restricted page (for example chrome://extensions). " +
                       "Switch to a normal http/https tab and try again.";
            }

            return "Indicate failed: " + (message ?? "Unknown error");
        }

        private Brush GetValidateBrush(bool background)
        {
            switch (ValidationState)
            {
                case ValidationState.Valid:
                    return background ? GreenBackground : GreenBorder;
                case ValidationState.Ambiguous:
                    return background ? OrangeBackground : OrangeBorder;
                case ValidationState.Invalid:
                    return background ? RedBackground : RedBorder;
                default:
                    return background ? GrayBackground : GrayBorder;
            }
        }
    }
}
