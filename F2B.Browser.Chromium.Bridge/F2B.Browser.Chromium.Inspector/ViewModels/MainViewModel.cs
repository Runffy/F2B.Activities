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
        private object[] _targetSegments;
        private InspectorSelectorLevel _selectedSelectorLevel;
        private InspectorVisualTreeNode _selectedVisualNode;
        private string _selectorXml = string.Empty;
        private string _targetElementDisplay = string.Empty;
        private string _connectionStatus = "Starting bridge server...";
        private ValidationState _validationState = ValidationState.None;
        private bool _hasCapturedElement;
        private bool _isIndicating;
        private bool _isHighlighting;
        private bool _isValidating;
        private bool _isTargetElementError;
        private bool _suppressSelectorUpdate;
        private bool _suppressVisualTreeSelection;

        public MainViewModel()
        {
            VisualTree = new ObservableCollection<InspectorVisualTreeNode>();
            SelectorLevels = new ObservableCollection<InspectorSelectorLevel>();
            PropertyExplorerItems = new ObservableCollection<InspectorPropertyItem>();
            SelectedItemProperties = new ObservableCollection<InspectorPropertyItem>();

            RefreshConnectionCommand = new RelayCommand(ReconnectAndRefreshStatus);
            IndicateCommand = new RelayCommand(() => _ = StartIndicateAsync(), () => _session.IsConnected && !IsIndicating && !IsHighlighting && !IsValidating);
            ValidateCommand = new RelayCommand(() => _ = ValidateSelectorAsync(), () => _hasCapturedElement && SelectorLevels.Count > 0 && !IsValidating && !IsHighlighting);
            HighlightCommand = new RelayCommand(() => _ = HighlightAsync(), () => !IsHighlighting && !IsIndicating && !IsValidating && SelectorLevels.Count > 0);
        }

        public ObservableCollection<InspectorVisualTreeNode> VisualTree { get; }
        public ObservableCollection<InspectorSelectorLevel> SelectorLevels { get; }
        public ObservableCollection<InspectorPropertyItem> PropertyExplorerItems { get; }
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

        public InspectorVisualTreeNode SelectedVisualNode
        {
            get => _selectedVisualNode;
            set
            {
                if (!SetProperty(ref _selectedVisualNode, value))
                    return;

                if (value == null || _suppressVisualTreeSelection)
                    return;

                _ = LoadNodeDetailsAsync(value);
            }
        }

        internal bool SuppressVisualTreeSelection => _suppressVisualTreeSelection;

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
            {
                SetTargetElementError(false);
                TargetElementDisplay = _hasCapturedElement
                    ? "Bridge reconnected. Click Validate to check the selector."
                    : string.Empty;
            }

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
                        TargetElementDisplay = FormatIndicateInvalidated(pick.InvalidatedReason);
                    }
                    else
                    {
                        SetTargetElementError(false);
                        TargetElementDisplay = pick.RestrictedUrl
                            ? "Indicate skipped: the active tab is a restricted Chrome page (for example chrome://extensions). Switch to a normal http/https page and try again."
                            : "Pick cancelled.";
                    }

                    return;
                }

                if (pick.Levels != null && pick.Levels.Count > 0)
                {
                    ApplyCapture(pick.Levels, pick.Segments, pick.DisplayName);
                    return;
                }

                var build = await Task.Run(() => tab.InspectorBuildSelector(pick.Segments));
                ApplyCapture(build.Levels, build.Segments, build.DisplayName);
            }
            catch (Exception ex)
            {
                TargetElementDisplay = FormatIndicateError(ex);
            }
            finally
            {
                if (session != null)
                    session.TargetTabRebound -= OnIndicateTargetTabRebound;

                session?.Dispose();

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

        private void OnIndicateTargetTabRebound(BwTab tab)
        {
            if (tab == null || _dispatcher == null)
                return;

            if (_dispatcher.CheckAccess())
                _targetTab = tab;
            else
                _dispatcher.Invoke(() => _targetTab = tab);
        }

        private void ApplyCapture(IList<SelectorLevel> levels, object[] segments, string displayName)
        {
            SetTargetElementError(false);
            _targetSegments = segments;
            _hasCapturedElement = levels != null && levels.Count > 0;
            TargetElementDisplay = displayName ?? string.Empty;

            SelectorLevels.Clear();
            SelectedItemProperties.Clear();

            foreach (var level in InspectorSelectorSerializer.FromBridgeLevels(levels))
            {
                AttachSelectorHandlers(level);
                SelectorLevels.Add(level);
            }

            SelectedSelectorLevel = SelectorLevels.LastOrDefault();
            UpdateSelectorXmlFromLevels();

            _postCaptureCts?.Cancel();
            _postCaptureCts?.Dispose();
            _postCaptureCts = new CancellationTokenSource();
            _ = ReloadVisualTreeAfterCaptureAsync(_postCaptureCts.Token);
        }

        private async Task ReloadVisualTreeAfterCaptureAsync(CancellationToken token)
        {
            try
            {
                await ReloadVisualTreeAsync(expandToTarget: false).ConfigureAwait(true);

                await Task.Delay(PostCaptureValidateDelayMs, token).ConfigureAwait(true);

                if (!token.IsCancellationRequested)
                    await ValidateSelectorAsync().ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task LoadNodeDetailsAsync(InspectorVisualTreeNode node)
        {
            if (node == null || _targetTab == null)
                return;

            try
            {
                var describe = await Task.Run(() => _targetTab.InspectorDescribe(node.Segments));
                PropertyExplorerItems.Clear();
                foreach (var property in describe.Properties ?? Enumerable.Empty<BridgeInspectorProperty>())
                {
                    PropertyExplorerItems.Add(new InspectorPropertyItem
                    {
                        Name = property.Name,
                        Value = property.Value,
                        IsSelected = true,
                        CanToggle = false
                    });
                }

                var build = await Task.Run(() => _targetTab.InspectorBuildSelector(node.Segments));
                ApplyCapture(build.Levels, build.Segments, build.DisplayName);
            }
            catch (Exception ex)
            {
                TargetElementDisplay = "Tree selection failed: " + ex.Message;
            }
        }

        private async Task ReloadVisualTreeAsync(bool expandToTarget = true)
        {
            if (_targetTab == null || _dispatcher == null)
                return;

            try
            {
                await _dispatcher.InvokeAsync(() => VisualTree.Clear());

                var htmlRoot = new InspectorVisualTreeNode("<html>", new object[0], new object[0], null);
                htmlRoot.RequestLoadChildren = LoadVisualTreeChildren;

                await _dispatcher.InvokeAsync(() => VisualTree.Add(htmlRoot));

                if (expandToTarget && _targetSegments != null && _targetSegments.Length > 0)
                    await ExpandVisualTreeToTargetAsync(htmlRoot);
                else
                    await _dispatcher.InvokeAsync(() => htmlRoot.IsExpanded = false);
            }
            catch (Exception ex)
            {
                if (!IsBridgeDisconnectError(ex))
                    TargetElementDisplay = "Failed to load visual tree: " + ex.Message;
            }
        }

        private async Task ExpandVisualTreeToTargetAsync(InspectorVisualTreeNode htmlRoot)
        {
            var target = _targetSegments;
            if (target == null || target.Length == 0)
                return;

            _suppressVisualTreeSelection = true;
            try
            {
                var current = htmlRoot;
                for (var depth = 0; depth < target.Length; depth++)
                {
                    await LoadVisualTreeChildrenAsync(current);

                    var prefix = new object[depth + 1];
                    Array.Copy(target, prefix, prefix.Length);

                    InspectorVisualTreeNode next = null;
                    await _dispatcher.InvokeAsync(() =>
                    {
                        current.IsExpanded = true;
                        next = current.Children.FirstOrDefault(child =>
                            VisualTreeSegmentHelper.SegmentsEqual(child.Segments, prefix));
                    });

                    if (next == null)
                        return;

                    current = next;
                }

                await LoadVisualTreeChildrenAsync(current);
                await _dispatcher.InvokeAsync(() =>
                {
                    current.IsExpanded = true;
                    current.IsSelected = true;
                    _selectedVisualNode = current;
                    RaisePropertyChanged(nameof(SelectedVisualNode));
                });
            }
            finally
            {
                _suppressVisualTreeSelection = false;
            }
        }

        private async Task LoadVisualTreeChildrenAsync(InspectorVisualTreeNode parentNode)
        {
            if (parentNode == null || parentNode.ChildrenLoaded || _targetTab == null)
                return;

            var nodes = await Task.Run(() => _targetTab.InspectorGetDomChildren(parentNode.LoadSegments));
            await _dispatcher.InvokeAsync(() =>
            {
                parentNode.Children.Clear();
                foreach (var node in nodes)
                {
                    var child = InspectorVisualTreeNode.FromDomNode(node, parentNode);
                    child.RequestLoadChildren = LoadVisualTreeChildren;
                    parentNode.Children.Add(child);
                }

                parentNode.ChildrenLoaded = true;
            });
        }

        private void LoadVisualTreeChildren(InspectorVisualTreeNode parentNode)
        {
            if (parentNode == null || parentNode.ChildrenLoaded || _targetTab == null)
                return;

            try
            {
                var nodes = _targetTab.InspectorGetDomChildren(parentNode.LoadSegments);
                parentNode.Children.Clear();
                foreach (var node in nodes)
                {
                    var child = InspectorVisualTreeNode.FromDomNode(node, parentNode);
                    child.RequestLoadChildren = LoadVisualTreeChildren;
                    parentNode.Children.Add(child);
                }

                parentNode.ChildrenLoaded = true;
            }
            catch (Exception ex)
            {
                TargetElementDisplay = "Failed to load tree children: " + ex.Message;
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
                TargetElementDisplay = "Validate skipped: Bridge extension is offline. Click Refresh Connection.";
                return;
            }

            IsValidating = true;
            try
            {
                var scope = SelectorXmlSerializer.SplitScope(SelectorXml);
                if (string.IsNullOrWhiteSpace(SelectorXmlSerializer.ToOperationXml(scope)))
                {
                    ValidationState = ValidationState.Invalid;
                    TargetElementDisplay = "Validate failed: no enabled selector levels.";
                    return;
                }

                var result = await CountSelectorMatchesWithRetryAsync().ConfigureAwait(true);
                ApplyValidationResult(result);
            }
            catch (Exception ex)
            {
                ValidationState = ValidationState.Invalid;
                TargetElementDisplay = FormatValidateException(ex);
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
                if (IsBridgeDisconnectMessage(TargetElementDisplay))
                    return;

                TargetElementDisplay = FormatValidateFailure(result);
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
            TargetElementDisplay = "Validate ambiguous: " + result.Count + " matches.";
        }

        private static string FormatValidateFailure(SelectorResolveResult result)
        {
            var message = "Validate failed: 0 matches within " + SelectorResolveRetry.DefaultTimeoutMilliseconds + "ms.";
            if (!string.IsNullOrWhiteSpace(result?.LastError))
                message += " " + result.LastError;

            return message;
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

        private static string FormatValidateException(Exception ex)
        {
            if (!IsBridgeDisconnectError(ex))
                return "Validate failed: " + ex.Message;

            return "Validate failed: Bridge connection lost (OpenRPA may have exited). " +
                   "Restart OpenRPA, click Refresh Connection, then Validate again.";
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
                    if (result.Count == 0)
                        TargetElementDisplay = "Highlight failed: element not found within 5 seconds.";
                    else
                        TargetElementDisplay = "Highlight failed: selector matched multiple elements.";

                    return;
                }

                var tab = _targetTab;
                await Task.Run(() => tab.InspectorHighlight(SelectorXml, HighlightDurationMs));
                await Task.Delay(HighlightDurationMs);
            }
            catch (Exception ex)
            {
                TargetElementDisplay = "Highlight failed: " + ex.Message;
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

            foreach (var property in level.Properties)
                SelectedItemProperties.Add(property);
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
            TargetElementDisplay = FormatIndicateError(exception);
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
