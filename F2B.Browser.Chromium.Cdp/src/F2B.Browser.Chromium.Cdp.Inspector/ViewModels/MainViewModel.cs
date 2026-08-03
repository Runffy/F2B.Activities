using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using F2B.Browser.Chromium.Cdp.Inspector.Helpers;
using F2B.Browser.Chromium.Cdp.Inspector.Models;
using F2B.Browser.Chromium.Cdp.Inspector.Overlays;
using F2B.Browser.Chromium.Cdp.Inspector.Services;
using F2B.Browser.Chromium.Cdp.Selectors;

namespace F2B.Browser.Chromium.Cdp.Inspector.ViewModels
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
        private const string IdleStatus =
            "Idle — connects only during Indicate / Validate / Highlight";

        private Dispatcher _dispatcher;
        private InspectorSelectorLevel _selectedSelectorLevel;
        private string _selectorXml = string.Empty;
        private string _targetElementDisplay = string.Empty;
        private string _connectionStatus = IdleStatus;
        private ValidationState _validationState = ValidationState.None;
        private bool _isIndicating;
        private bool _isHighlighting;
        private bool _isValidating;
        private bool _isTargetElementError;
        private bool _suppressSelectorUpdate;
        private bool _suppressTargetElementStatusMessage;
        private AnalyzingOverlay _analyzingOverlay;

        public MainViewModel()
        {
            SelectorLevels = new ObservableCollection<InspectorSelectorLevel>();
            SelectedItemProperties = new ObservableCollection<InspectorPropertyItem>();

            IndicateCommand = new RelayCommand(() => _ = StartIndicateAsync(), () => !IsIndicating && !IsHighlighting && !IsValidating);
            ValidateCommand = new RelayCommand(() => _ = ValidateSelectorAsync(), () => !string.IsNullOrWhiteSpace(SelectorXml) && !IsValidating && !IsHighlighting);
            HighlightCommand = new RelayCommand(() => _ = HighlightAsync(), () => !string.IsNullOrWhiteSpace(SelectorXml) && !IsHighlighting && !IsIndicating && !IsValidating);
            InsertParentStepCommand = new RelayCommand(InsertParentStep, () => SelectorLevels.Count > 0 || !string.IsNullOrWhiteSpace(SelectorXml));
        }

        public ObservableCollection<InspectorSelectorLevel> SelectorLevels { get; }
        public ObservableCollection<InspectorPropertyItem> SelectedItemProperties { get; }

        public RelayCommand IndicateCommand { get; }
        public RelayCommand ValidateCommand { get; }
        public RelayCommand HighlightCommand { get; }
        public RelayCommand InsertParentStepCommand { get; }

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
            ConnectionStatus = IdleStatus;
        }

        public void Cleanup()
        {
            // No persistent CDP session — connections are short-lived only.
            ConnectionStatus = IdleStatus;
        }

        public void ReportRuntimeError(Exception exception)
        {
            SetTargetElementError(true);
            TargetElementDisplay = FormatError(exception);
        }

        private async Task StartIndicateAsync()
        {
            IsIndicating = true;
            ValidationState = ValidationState.None;
            ConnectionStatus = "Indicating — short-lived CDP attach while picking";

            var window = Application.Current?.MainWindow;
            if (window != null)
                window.WindowState = WindowState.Minimized;

            CdpIndicateSession session = null;
            try
            {
                session = new CdpIndicateSession(_dispatcher);
                var pick = await session.RunAsync(window).ConfigureAwait(true);
                if (pick.Cancelled)
                {
                    SetTargetElementError(false);
                    TargetElementDisplay = string.IsNullOrEmpty(pick.InvalidatedReason)
                        ? "Pick cancelled."
                        : FormatIndicateInvalidated(pick.InvalidatedReason);
                    return;
                }

                ShowAnalyzingOverlay();
                _suppressTargetElementStatusMessage = true;
                ApplyCapture(pick.Levels, pick.DisplayName, pick.MinimalLevels);
                await Task.Delay(PostCaptureValidateDelayMs).ConfigureAwait(true);
                await ValidateSelectorAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                SetTargetElementError(true);
                TargetElementDisplay = "Indicate failed: " + FormatError(ex);
            }
            finally
            {
                _suppressTargetElementStatusMessage = false;
                session?.Dispose();
                HideAnalyzingOverlay();

                if (window != null && _dispatcher != null)
                    await _dispatcher.InvokeAsync(() => WindowRestoreHelper.RestoreAfterIndicateComplete(window));
                else if (window != null)
                    window.WindowState = WindowState.Normal;

                ConnectionStatus = IdleStatus;
                IsIndicating = false;
            }
        }

        private async Task ValidateSelectorAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectorXml))
                return;

            SyncSelectorLevelsFromXml();
            IsValidating = true;
            ConnectionStatus = "Validating — short-lived CDP attach";
            try
            {
                var result = await SelectorResolveRetry.CountMatchesWithRetryAsync(
                    () => Task.Run(() =>
                    {
                        var resolved = CdpShortLivedResolver.CountMatches(SelectorXml, keepSessionAlive: false);
                        if (!string.IsNullOrEmpty(resolved.Error) && resolved.MatchCount == 0)
                            throw new InvalidOperationException(resolved.Error);
                        return resolved.MatchCount;
                    }),
                    ResolveTimeoutMs,
                    ResolveIntervalMs).ConfigureAwait(true);

                ApplyValidationResult(result);
            }
            catch (Exception ex)
            {
                ValidationState = ValidationState.Invalid;
                SetTargetElementError(true);
                if (!_suppressTargetElementStatusMessage)
                    TargetElementDisplay = FormatError(ex);
            }
            finally
            {
                ConnectionStatus = IdleStatus;
                IsValidating = false;
            }
        }

        private async Task HighlightAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectorXml))
                return;

            SyncSelectorLevelsFromXml();
            IsHighlighting = true;
            ConnectionStatus = "Highlighting — short-lived CDP attach";
            CdpShortLivedResolver.ResolveResult alive = null;
            try
            {
                var countResult = await SelectorResolveRetry.CountMatchesWithRetryAsync(
                    () => Task.Run(() =>
                    {
                        var resolved = CdpShortLivedResolver.CountMatches(SelectorXml, keepSessionAlive: false);
                        if (!string.IsNullOrEmpty(resolved.Error) && resolved.MatchCount == 0)
                            throw new InvalidOperationException(resolved.Error);
                        return resolved.MatchCount;
                    }),
                    ResolveTimeoutMs,
                    ResolveIntervalMs).ConfigureAwait(true);

                if (countResult.Count != 1)
                {
                    ApplyValidationResult(countResult);
                    SetTargetElementError(true);
                    return;
                }

                alive = await Task.Run(() => CdpShortLivedResolver.CountMatches(SelectorXml, keepSessionAlive: true))
                    .ConfigureAwait(true);
                if (alive.MatchCount != 1 || alive.FirstElement == null || alive.Tab == null)
                {
                    ValidationState = ValidationState.Invalid;
                    SetTargetElementError(true);
                    return;
                }

                ValidationState = ValidationState.Valid;
                SetTargetElementError(false);
                await Task.Run(() =>
                    CdpPagePickAssist.HighlightMatchedElement(alive.Tab, alive.FirstElement, HighlightDurationMs))
                    .ConfigureAwait(true);
                await Task.Delay(HighlightDurationMs).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                SetTargetElementError(true);
                TargetElementDisplay = FormatError(ex);
            }
            finally
            {
                if (alive != null)
                    CdpShortLivedResolver.DisposeQuietly(alive.Browser);
                ConnectionStatus = IdleStatus;
                IsHighlighting = false;
            }
        }

        internal void ApplyCapture(
            IList<SelectorLevel> levels,
            string displayName,
            IList<SelectorLevel> minimalLevels = null)
        {
            SetTargetElementError(false);
            if (!_suppressTargetElementStatusMessage || !string.IsNullOrEmpty(displayName))
                TargetElementDisplay = displayName ?? string.Empty;

            SelectorLevels.Clear();
            SelectedItemProperties.Clear();

            foreach (var level in InspectorSelectorSerializer.FromLevels(levels))
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

            DesignerIntegrationStub.TryWriteSelectorFile(SelectorXml);
        }

        private void ApplyValidationResult(SelectorResolveResult result)
        {
            if (result == null)
                result = SelectorResolveResult.None;

            if (result.Count <= 0)
            {
                ValidationState = ValidationState.Invalid;
                SetTargetElementError(true);
                return;
            }

            if (result.Count == 1)
            {
                ValidationState = ValidationState.Valid;
                SetTargetElementError(false);
                return;
            }

            ValidationState = ValidationState.Ambiguous;
            SetTargetElementError(true);
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

        private void SyncSelectorLevelsFromXml()
        {
            if (string.IsNullOrWhiteSpace(SelectorXml))
                return;

            var parsed = SelectorXmlSerializer.Deserialize(SelectorXml);
            if (parsed.Count == 0)
                return;

            _suppressSelectorUpdate = true;
            SelectorLevels.Clear();
            SelectedItemProperties.Clear();

            foreach (var level in InspectorSelectorSerializer.FromLevels(parsed))
            {
                AttachSelectorHandlers(level);
                SelectorLevels.Add(level);
            }

            if (SelectedSelectorLevel == null || !SelectorLevels.Contains(SelectedSelectorLevel))
                SelectedSelectorLevel = SelectorLevels.LastOrDefault();

            _suppressSelectorUpdate = false;
        }

        private void InsertParentStep()
        {
            if (SelectorLevels.Count == 0 && !string.IsNullOrWhiteSpace(SelectorXml))
                SyncSelectorLevelsFromXml();

            if (SelectorLevels.Count == 0)
                return;

            var insertIndex = SelectedSelectorLevel != null
                ? SelectorLevels.IndexOf(SelectedSelectorLevel) + 1
                : SelectorLevels.Count;

            var parentLevel = InspectorSelectorSerializer.CreateParentLevel(1);
            AttachSelectorHandlers(parentLevel);

            if (insertIndex >= SelectorLevels.Count)
                SelectorLevels.Add(parentLevel);
            else
                SelectorLevels.Insert(insertIndex, parentLevel);

            SelectedSelectorLevel = parentLevel;
            UpdateSelectorXmlFromLevels();
        }

        private void MarkValidationStale()
        {
            if (ValidationState != ValidationState.None)
                ValidationState = ValidationState.None;
        }

        private void SetTargetElementError(bool isError)
        {
            if (_isTargetElementError == isError)
                return;

            _isTargetElementError = isError;
            RaisePropertyChanged(nameof(TargetElementForeground));
        }

        private static string FormatIndicateInvalidated(string reason)
        {
            switch (reason)
            {
                case IndicateInvalidatedReason.TabClosed:
                    return "Indicate 已取消：目标标签页已关闭。";
                case IndicateInvalidatedReason.RestrictedTab:
                    return "Indicate 已取消：当前标签页为受限页面。请切换到普通 http/https 页面后重新 Indicate。";
                default:
                    return "Indicate 已取消：" + reason;
            }
        }

        private static string FormatError(Exception exception)
        {
            var message = exception is AggregateException aggregate
                ? aggregate.Flatten().InnerException?.Message ?? aggregate.Message
                : exception?.Message;

            return message ?? "Unknown error";
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
